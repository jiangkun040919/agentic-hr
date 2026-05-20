using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AIRecruitment.Api.Services;

public class RabbitMQConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<RabbitMQConsumerService> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMQConsumerService(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<RabbitMQConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _config["RabbitMQ:Host"] ?? "localhost",
                UserName = _config["RabbitMQ:UserName"] ?? "guest",
                Password = _config["RabbitMQ:Password"] ?? "guest"
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            string[] queues = { "ai_resume_analyze", "email_notification", "report_generation" };
            foreach (var q in queues)
                _channel.QueueDeclare(q, durable: true, exclusive: false, autoDelete: false);

            _channel.BasicQos(0, 1, false);

            var aiConsumer = new EventingBasicConsumer(_channel);
            aiConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var msg = JsonSerializer.Deserialize<JsonElement>(body);
                    var deliveryId = msg.GetProperty("DeliveryId").GetInt32();

                    _logger.LogInformation("Consuming ai_resume_analyze: DeliveryId={DeliveryId}", deliveryId);

                    using var scope = _scopeFactory.CreateScope();
                    var aiSvc = scope.ServiceProvider.GetRequiredService<IAIService>();
                    await aiSvc.AnalyzeResumeAsync(deliveryId);
                    await aiSvc.ScoreResumeAsync(deliveryId);

                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing ai_resume_analyze");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };
            _channel.BasicConsume("ai_resume_analyze", false, aiConsumer);

            var emailConsumer = new EventingBasicConsumer(_channel);
            emailConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.SysOperLogs.Add(new Models.SysOperLog
                    {
                        Module = "EmailNotification",
                        Action = "Send",
                        Detail = body[..Math.Min(body.Length, 500)],
                        CreatedAt = DateTime.Now
                    });
                    await db.SaveChangesAsync();
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing email_notification");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };
            _channel.BasicConsume("email_notification", false, emailConsumer);

            var reportConsumer = new EventingBasicConsumer(_channel);
            reportConsumer.Received += async (_, ea) =>
            {
                try
                {
                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.SysOperLogs.Add(new Models.SysOperLog
                    {
                        Module = "ReportGeneration",
                        Action = "Generate",
                        Detail = body[..Math.Min(body.Length, 500)],
                        CreatedAt = DateTime.Now
                    });
                    await db.SaveChangesAsync();
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing report_generation");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };
            _channel.BasicConsume("report_generation", false, reportConsumer);

            _logger.LogInformation("RabbitMQ Consumer started. Listening on 3 queues.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ Consumer failed to start (RabbitMQ may not be installed)");
        }

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
