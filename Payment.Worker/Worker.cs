using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Payment.Worker
{
    public class Worker : BackgroundService
    {
        private IConnection _connection;
        private IModel _channel;

        private const string QueueName = "payment-queue";

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory()
            {
                HostName = "localhost"
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.BasicQos(0, 1, false);

            var consumer = new EventingBasicConsumer(_channel);

            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Console.WriteLine($"Processing: {message}");

                //simulation heavy job
                Thread.Sleep(5000);
                _channel.BasicAck(ea.DeliveryTag, false);

            };

            _channel.BasicConsume(
                queue: QueueName,
                autoAck: false,
                consumer: consumer);

            return Task.CompletedTask;
        }
        public override void Dispose() 
        {
            _channel?.Close();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}
