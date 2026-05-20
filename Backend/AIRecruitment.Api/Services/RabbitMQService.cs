using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace AIRecruitment.Api.Services;

public interface IRabbitMQService
{
    Task PublishAsync(string queueName, object message);
}

public class RabbitMQService : IRabbitMQService
{
    private readonly IModel? _channel;

    public RabbitMQService(IConfiguration configuration)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:Host"] ?? "localhost",
                UserName = configuration["RabbitMQ:UserName"] ?? "guest",
                Password = configuration["RabbitMQ:Password"] ?? "guest"
            };
            var connection = factory.CreateConnection();
            _channel = connection.CreateModel();

            _channel.QueueDeclare("ai_resume_analyze", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueDeclare("email_notification", durable: true, exclusive: false, autoDelete: false);
            _channel.QueueDeclare("report_generation", durable: true, exclusive: false, autoDelete: false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RabbitMQ] 连接失败（不影响启动）: {ex.Message}");
        }
    }

    public Task PublishAsync(string queueName, object message)
    {
        if (_channel == null) return Task.CompletedTask;

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;

        _channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: properties, body: body);
        return Task.CompletedTask;
    }
}
