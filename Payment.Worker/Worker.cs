using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Payment.Worker
{
    public class Worker : BackgroundService
    {
        private IConnection _connection;
        private IModel _channel;

        private const string MainExchange = "payment-exchange";
        private const string RetryExchange = "retry-exchange";
        private const string DlxExchange = "dlx-exchange";

        private const string MainQueue = "payment-queue";
        private const string RetryQueue = "payment-retry-queue";
        private const string DeadLetterQueue = "payment-dlq";

        private const string RoutingKey = "payment.created";
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory()
            {
                HostName = "localhost"
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            DeclareInfrastructure();

            _channel.BasicQos(0, 1, false);

            var consumer = new EventingBasicConsumer(_channel);

            consumer.Received += (model, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    Console.WriteLine($"Processing: {json}");

                    if (json.Contains("Fail"))
                        throw new Exception("Simulated failure");

                    Thread.Sleep(2000);

                    Console.WriteLine("Success->ACK");
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception)
                {
                    HandleRetry(ea);
                }

            };

            _channel.BasicConsume(
            queue: MainQueue,
            autoAck: false,
            consumer: consumer);

            return Task.CompletedTask;
        }
        private void DeclareInfrastructure()
        {
            // Exchanges
            _channel.ExchangeDeclare(MainExchange, ExchangeType.Direct, true);
            _channel.ExchangeDeclare(RetryExchange, ExchangeType.Direct, true);
            _channel.ExchangeDeclare(DlxExchange, ExchangeType.Direct, true);

            // MAIN QUEUE → hata olursa retry exchange'e gider
            var mainArgs = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", RetryExchange }
        };

            _channel.QueueDeclare(MainQueue, true, false, false, mainArgs);
            _channel.QueueBind(MainQueue, MainExchange, RoutingKey);

            // RETRY QUEUE → 5 sn bekle → tekrar main exchange'e dön
            var retryArgs = new Dictionary<string, object>
        {
            { "x-message-ttl", 5000 },
            { "x-dead-letter-exchange", MainExchange }
        };

            _channel.QueueDeclare(RetryQueue, true, false, false, retryArgs);
            _channel.QueueBind(RetryQueue, RetryExchange, RoutingKey);

            // DEAD LETTER QUEUE
            _channel.QueueDeclare(DeadLetterQueue, true, false, false);
            _channel.QueueBind(DeadLetterQueue, DlxExchange, RoutingKey);
        }

        private void HandleRetry(BasicDeliverEventArgs ea)
        {
            var retryCount = 0;

            if (ea.BasicProperties.Headers != null &&
                ea.BasicProperties.Headers.ContainsKey("x-retry-count"))
            {
                retryCount = Convert.ToInt32(
                    Encoding.UTF8.GetString(
                        (byte[])ea.BasicProperties.Headers["x-retry-count"]));
            }

            if (retryCount >= 3)
            {
                Console.WriteLine("Max retry reached → sending to DLQ");

                var dlqProps = _channel.CreateBasicProperties();
                dlqProps.Persistent = true;

                _channel.BasicPublish(
                    DlxExchange,
                    RoutingKey,
                    dlqProps,
                    ea.Body);

                _channel.BasicAck(ea.DeliveryTag, false);
                return;
            }

            retryCount++;

            Console.WriteLine($"Retry attempt {retryCount}");

            var props = _channel.CreateBasicProperties();
            props.Persistent = true;
            props.Headers = new Dictionary<string, object>
        {
            { "x-retry-count", Encoding.UTF8.GetBytes(retryCount.ToString()) }
        };

            _channel.BasicPublish(
                RetryExchange,
                RoutingKey,
                props,
                ea.Body);

            _channel.BasicAck(ea.DeliveryTag, false);
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}
