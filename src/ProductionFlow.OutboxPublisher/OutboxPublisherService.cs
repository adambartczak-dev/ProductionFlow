using System.Text;
using Microsoft.EntityFrameworkCore;
using ProductionFlow.Api.Data;
using ProductionFlow.Messaging;
using RabbitMQ.Client;

namespace ProductionFlow.OutboxPublisher;

public sealed class OutboxPublisherService : BackgroundService
{
    private const int BatchSize = 20;

    private static readonly TimeSpan PollInterval =
        TimeSpan.FromSeconds(3);

    private static readonly TimeSpan RetryInterval =
        TimeSpan.FromSeconds(5);

    private static readonly TimeSpan ConfirmTimeout =
        TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherService> _logger;

    public OutboxPublisherService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Uruchomiono ProductionFlow OutboxPublisher.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var settings = RabbitMqSettings.FromEnvironment();

                var factory = new ConnectionFactory
                {
                    HostName = settings.Host,
                    Port = settings.Port,
                    UserName = settings.UserName,
                    Password = settings.Password,

                    // Po awarii sami odtwarzamy połączenie.
                    AutomaticRecoveryEnabled = false,

                    RequestedConnectionTimeout =
                        TimeSpan.FromSeconds(5)
                };

                await using var connection =
                    await factory.CreateConnectionAsync(stoppingToken);

                // Publisher Confirms + śledzenie potwierdzeń.
                var channelOptions = new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true);

                await using var channel =
                    await connection.CreateChannelAsync(
                        channelOptions,
                        stoppingToken);

                // Ta sama topologia co w Dniu 39.
                await channel.ExchangeDeclareAsync(
                    exchange: RabbitMqTopology.Exchange,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(
                    queue: RabbitMqTopology.Queue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await channel.QueueBindAsync(
                    queue: RabbitMqTopology.Queue,
                    exchange: RabbitMqTopology.Exchange,
                    routingKey: RabbitMqTopology.RoutingKey,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "Połączono z RabbitMQ. Oczekiwanie na Outbox.");

                // Odczytujemy wiadomości, dopóki połączenie działa.
                while (!stoppingToken.IsCancellationRequested &&
                       connection.IsOpen &&
                       channel.IsOpen)
                {
                    int publishedCount = await PublishBatchAsync(
                        channel,
                        stoppingToken);

                    // Gdy jest więcej pracy, następna paczka
                    // jest pobierana bez długiego oczekiwania.
                    var delay = publishedCount > 0
                        ? TimeSpan.FromMilliseconds(250)
                        : PollInterval;

                    await Task.Delay(delay, stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Błąd publikowania Outbox. " +
                    "Ponowienie za {Seconds} sekund.",
                    RetryInterval.TotalSeconds);
            }

            // Ponawianie po problemie z RabbitMQ lub PostgreSQL.
            try
            {
                await Task.Delay(RetryInterval, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation(
            "Zatrzymano ProductionFlow OutboxPublisher.");
    }

    private async Task<int> PublishBatchAsync(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        // DbContext jest usługą Scoped.
        await using var scope = _scopeFactory.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        // Pobieramy wyłącznie wiadomości nieopublikowane.
        var messages = await db.OutboxMessages
            .Where(message => message.PublishedAtUtc == null)
            .OrderBy(message => message.OccurredAtUtc)
            .ThenBy(message => message.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return 0;
        }

        int publishedCount = 0;

        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // MessageId jest stały również przy ponowieniu.
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                Persistent = true,
                MessageId = message.Id.ToString()
            };

            byte[] body = Encoding.UTF8.GetBytes(message.Payload);

            // Ograniczamy oczekiwanie na potwierdzenie.
            using var confirmCts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            confirmCts.CancelAfter(ConfirmTimeout);

            // Dzięki Publisher Confirms await czeka
            // na potwierdzenie RabbitMQ.
            await channel.BasicPublishAsync(
                exchange: RabbitMqTopology.Exchange,
                routingKey: RabbitMqTopology.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: confirmCts.Token);

            // Zmieniamy stan dopiero po potwierdzeniu.
            message.MarkAsPublished();

            await db.SaveChangesAsync(cancellationToken);

            publishedCount++;

            _logger.LogInformation(
                "Opublikowano zdarzenie {EventType}. " +
                "EventId={EventId}, OrderId={OrderId}",
                message.EventType,
                message.Id,
                message.OrderId);
        }

        return publishedCount;
    }
}