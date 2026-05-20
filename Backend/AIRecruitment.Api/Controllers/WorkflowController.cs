using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIRecruitment.Api.Models;
using AIRecruitment.Api.Services;

namespace AIRecruitment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowEngine _engine;

    public WorkflowController(IWorkflowEngine engine) { _engine = engine; }

    [HttpGet("definitions")]
    [Authorize(Roles = "hr")]
    public async Task<IActionResult> GetDefinitions()
    {
        try
        {
            var result = await _engine.GetDefinitionsAsync();
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return BadRequest(new { code = 400, message = ex.Message }); }
    }

    [HttpPost("definitions")]
    [Authorize(Roles = "hr,admin")]
    public async Task<IActionResult> CreateDefinition([FromBody] WorkflowDefinition definition)
    {
        try
        {
            var result = await _engine.CreateDefinitionAsync(definition);
            return Ok(new { code = 200, message = "工作流定义已创建", data = result });
        }
        catch (Exception ex) { return BadRequest(new { code = 400, message = ex.Message }); }
    }

    [HttpPost("instances")]
    [Authorize(Roles = "hr")]
    public async Task<IActionResult> CreateInstance([FromBody] CreateInstanceRequest request)
    {
        try
        {
            var result = await _engine.CreateInstanceAsync(request.DefinitionId, request.EntityId, request.EntityType, request.InitialState);
            return Ok(new { code = 200, message = "工作流实例已创建", data = result });
        }
        catch (Exception ex) { return BadRequest(new { code = 400, message = ex.Message }); }
    }

    [HttpGet("instances/{id}")]
    [Authorize(Roles = "hr")]
    public async Task<IActionResult> GetInstance(int id)
    {
        try
        {
            var result = await _engine.GetInstanceAsync(id);
            if (result == null) return NotFound(new { code = 404, message = "实例不存在" });
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return BadRequest(new { code = 400, message = ex.Message }); }
    }

    [HttpPost("instances/{id}/advance")]
    [Authorize(Roles = "hr")]
    public async Task<IActionResult> AdvanceInstance(int id)
    {
        try
        {
            var result = await _engine.AdvanceAsync(id);
            return Ok(new { code = 200, message = "工作流已推进", data = result });
        }
        catch (Exception ex) { return BadRequest(new { code = 400, message = ex.Message }); }
    }

    [HttpPost("instances/{id}/step/{stepName}")]
    [Authorize(Roles = "hr")]
    public async Task<IActionResult> TriggerStep(int id, string stepName, [FromBody] object? input = null)
    {
        try
        {
            var result = await _engine.TriggerStepAsync(id, stepName, input);
            return Ok(new { code = 200, message = "步骤已执行", data = result });
        }
        catch (Exception ex) { return BadRequest(new { code = 400, message = ex.Message }); }
    }

    [HttpGet("instances")]
    [Authorize(Roles = "hr")]
    public async Task<IActionResult> GetInstancesByEntity([FromQuery] string entityType, [FromQuery] int entityId)
    {
        try
        {
            var result = await _engine.GetInstancesByEntityAsync(entityType, entityId);
            return Ok(new { code = 200, data = result });
        }
        catch (Exception ex) { return BadRequest(new { code = 400, message = ex.Message }); }
    }
}

public record CreateInstanceRequest(int DefinitionId, int EntityId, string EntityType, object? InitialState = null);
