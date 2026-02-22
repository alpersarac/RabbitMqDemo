using System.Text.Json;
using System.Text;
using Payment.Api.Contracts;
using RabbitMQ.Client;

namespace Payment.Api.Messaging
{
    public class RabbitPublisher
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;

        private const string ExchangeName = "payment-exchange";
        private const string QueueName = "payment-queue";
        private const string RoutingKey = "payment.created";

        public RabbitPublisher()
        {
            var factory = new ConnectionFactory()
            {
                HostName = "localhost"
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare(
            ExchangeName,
            ExchangeType.Direct,
            durable: true);

            _channel.QueueDeclare(
                QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false);

            _channel.QueueBind(
                QueueName,
                ExchangeName,
                RoutingKey);
        }
        public void Publish(PaymentMessage message)
        {
            var body = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(message));

            var props = _channel.CreateBasicProperties();
            props.Persistent = true;

            _channel.BasicPublish(
                ExchangeName,
                RoutingKey,
                props,
                body);
        }
        public void Dispose()
        {
            _channel.Dispose();
            _connection.Dispose();
        }
    }
}
