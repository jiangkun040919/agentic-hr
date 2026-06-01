using System.Text.Json;
using AIRecruitment.Api.Hubs;
using AIRecruitment.Api.Models;

namespace AIRecruitment.Api.Services;

public interface IWorkflowEngine
{
    Task<WorkflowInstance> CreateInstanceAsync(int definitionId, int entityId, string entityType, object? initialState = null);
    Task<WorkflowInstance> AdvanceAsync(int instanceId);
    Task<WorkflowInstance> TriggerStepAsync(int instanceId, string stepName, object? input = null);
    Task<WorkflowInstance?> GetInstanceAsync(int instanceId);
    Task<List<WorkflowInstance>> GetInstancesByEntityAsync(string entityType, int entityId);
    Task<List<WorkflowDefinition>> GetDefinitionsAsync();
    Task<WorkflowDefinition> CreateDefinitionAsync(WorkflowDefinition definition);
}

public class WorkflowEngine : IWorkflowEngine
{
    private readonly AppDbContext _db;
    private readonly IEnumerable<IWorkflowStepHandler> _handlers;
    private readonly ISignalRService _signalR;

    public WorkflowEngine(AppDbContext db, IEnumerable<IWorkflowStepHandler> handlers, ISignalRService signalR)
    {
        _db = db;
        _handlers = handlers;
        _signalR = signalR;
    }

    public async Task<WorkflowInstance> CreateInstanceAsync(int definitionId, int entityId, string entityType, object? initialState = null)
    {
        var definition = await _db.WorkflowDefinitions.FindAsync(definitionId)
            ?? throw new Exception($"Workflow definition {definitionId} not found");

        var instance = new WorkflowInstance
        {
            DefinitionId = definitionId,
            EntityId = entityId,
            EntityType = entityType,
            CurrentStep = "",
            Status = 0,
            StateJson = JsonSerializer.Serialize(initialState ?? new { DeliveryId = entityId })
        };

        _db.WorkflowInstances.Add(instance);
        await _db.SaveChangesAsync();

        return await AdvanceAsync(instance.InstanceId);
    }

    public async Task<WorkflowInstance> AdvanceAsync(int instanceId)
    {
        var instance = await _db.WorkflowInstances.FindAsync(instanceId)
            ?? throw new Exception($"Instance {instanceId} not found");

        var definition = await _db.WorkflowDefinitions.FindAsync(instance.DefinitionId)
            ?? throw new Exception($"Definition {instance.DefinitionId} not found");

        var steps = JsonSerializer.Deserialize<List<WorkflowStep>>(definition.StepsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<WorkflowStep>();

        WorkflowStep? currentStep;
        if (string.IsNullOrEmpty(instance.CurrentStep))
        {
            currentStep = steps.FirstOrDefault()
                ?? throw new Exception("No steps defined in workflow");
        }
        else
        {
            var curIdx = steps.FindIndex(s => s.StepName == instance.CurrentStep);
            if (curIdx < 0) throw new Exception($"Step '{instance.CurrentStep}' not found");

            var curStep = steps[curIdx];
            if (curStep.NextSteps.Length == 0)
            {
                instance.Status = 2;
                instance.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await NotifyAsync(instance, curStep, true);
                return instance;
            }

            var nextName = curStep.NextSteps[0];
            currentStep = steps.FirstOrDefault(s => s.StepName == nextName)
                ?? throw new Exception($"Next step '{nextName}' not found");
        }

        return await ExecuteStepAsync(instance, currentStep, steps);
    }

    public async Task<WorkflowInstance> TriggerStepAsync(int instanceId, string stepName, object? input = null)
    {
        var instance = await _db.WorkflowInstances.FindAsync(instanceId)
            ?? throw new Exception($"Instance {instanceId} not found");

        var definition = await _db.WorkflowDefinitions.FindAsync(instance.DefinitionId)
            ?? throw new Exception($"Definition {instance.DefinitionId} not found");

        var steps = JsonSerializer.Deserialize<List<WorkflowStep>>(definition.StepsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<WorkflowStep>();

        var step = steps.FirstOrDefault(s => s.StepName == stepName)
            ?? throw new Exception($"Step '{stepName}' not found");

        if (input != null)
            instance.StateJson = JsonSerializer.Serialize(input);

        return await ExecuteStepAsync(instance, step, steps, input);
    }

    private async Task<WorkflowInstance> ExecuteStepAsync(WorkflowInstance instance, WorkflowStep step,
        List<WorkflowStep> steps, object? input = null)
    {
        instance.Status = 1;
        instance.CurrentStep = step.StepName;

        var log = new WorkflowStepLog
        {
            InstanceId = instance.InstanceId,
            StepName = step.StepName,
            Action = step.StepType,
            Status = 1,
            StartedAt = DateTime.UtcNow
        };
        _db.WorkflowStepLogs.Add(log);
        await _db.SaveChangesAsync();

        try
        {
            var handler = _handlers.FirstOrDefault(h => h.CanHandle(step.StepType))
                ?? throw new Exception($"No handler for step type '{step.StepType}'");

            var stateObj = string.IsNullOrEmpty(instance.StateJson) ? null
                : JsonSerializer.Deserialize<object>(instance.StateJson);

            var result = await handler.ExecuteAsync(step, input ?? stateObj);

            log.Status = result.Success ? 2 : 3;
            log.ResultJson = JsonSerializer.Serialize(result.Data);
            log.CompletedAt = DateTime.UtcNow;

            if (result.Success)
            {
                instance.StateJson = JsonSerializer.Serialize(result.Data);

                if (step.NextSteps.Length > 0)
                {
                    var nextName = result.NextStep ?? step.NextSteps[0];
                    instance.CurrentStep = nextName;
                    instance.Status = 1;

                    await _db.SaveChangesAsync();
                    await NotifyAsync(instance, step, true);

                    var nextStep = steps.FirstOrDefault(s => s.StepName == nextName);
                    if (nextStep != null)
                        return await ExecuteStepAsync(instance, nextStep, steps);
                }
                else
                {
                    instance.Status = 2;
                    instance.CompletedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();
                await NotifyAsync(instance, step, true);
            }
            else
            {
                instance.Status = 3;
                instance.ErrorMessage = result.Error;
                await _db.SaveChangesAsync();
                await NotifyAsync(instance, step, false, result.Error);
            }
        }
        catch (Exception ex)
        {
            log.Status = 3;
            log.ResultJson = ex.Message;
            log.CompletedAt = DateTime.UtcNow;
            instance.Status = 3;
            instance.ErrorMessage = ex.Message;
            await _db.SaveChangesAsync();
            await NotifyAsync(instance, step, false, ex.Message);
        }

        return instance;
    }

    private async Task NotifyAsync(WorkflowInstance instance, WorkflowStep step, bool success, string? error = null)
    {
        await _signalR.SendToAllAsync(NotificationEvents.WorkflowStepCompleted, new
        {
            instanceId = instance.InstanceId,
            entityType = instance.EntityType,
            entityId = instance.EntityId,
            step = step.StepName,
            success,
            error,
            status = instance.Status
        });
    }

    public Task<WorkflowInstance?> GetInstanceAsync(int instanceId)
    {
        return _db.WorkflowInstances.FindAsync(instanceId).AsTask();
    }

    public Task<List<WorkflowInstance>> GetInstancesByEntityAsync(string entityType, int entityId)
    {
        return Task.FromResult(_db.WorkflowInstances
            .Where(i => i.EntityType == entityType && i.EntityId == entityId)
            .OrderByDescending(i => i.CreatedAt)
            .ToList());
    }

    public Task<List<WorkflowDefinition>> GetDefinitionsAsync()
    {
        return Task.FromResult(_db.WorkflowDefinitions
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .ToList());
    }

    public async Task<WorkflowDefinition> CreateDefinitionAsync(WorkflowDefinition definition)
    {
        _db.WorkflowDefinitions.Add(definition);
        await _db.SaveChangesAsync();
        return definition;
    }
}
