using System.Text.Json;
using ProductionFlow.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ProductionFlow.Worker;

public sealed class OrderEventsWorker : BackgroundService
{
    private const int MaxRetries = 2;
    private const int RetryDelayMs = 5000;

    private static readonly TimeSpan PublishTimeout =
        TimeSpan.FromSeconds(10);

    private readonly ILogger<OrderEventsWorker> _logger;

    // NOWE: osobna klasa przetwarzająca zdarzenia.
    private readonly OrderEventProcessor _processor;

    public OrderEventsWorker(
        ILogger<OrderEventsWorker> logger,
        OrderEventProcessor processor)
    {
        _logger = logger;
        _processor = processor;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var settings = RabbitMqSettings.FromEnvironment();

        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
            AutomaticRecoveryEnabled = false
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection =
                    await factory.CreateConnectionAsync(stoppingToken);

                await using var consumerChannel =
                    await connection.CreateChannelAsync(
                        cancellationToken: stoppingToken);

                var publisherOptions = new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true);

                await using var publisherChannel =
                    await connection.CreateChannelAsync(
                        publisherOptions,
                        stoppingToken);

                // Dotychczasowy Exchange.
                await consumerChannel.ExchangeDeclareAsync(
                    exchange: RabbitMqTopology.Exchange,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                // Główna kolejka.
                await consumerChannel.QueueDeclareAsync(
                    queue: RabbitMqTopology.Queue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await consumerChannel.QueueBindAsync(
                    queue: RabbitMqTopology.Queue,
                    exchange: RabbitMqTopology.Exchange,
                    routingKey: RabbitMqTopology.RoutingKey,
                    cancellationToken: stoppingToken);

                // Kolejka Retry — Dzień 43.
                var retryArguments = new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = RetryDelayMs,
                    ["x-dead-letter-exchange"] =
                        RabbitMqTopology.Exchange,
                    ["x-dead-letter-routing-key"] =
                        RabbitMqTopology.RoutingKey
                };

                await consumerChannel.QueueDeclareAsync(
                    queue: RabbitMqTopology.RetryQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: retryArguments,
                    cancellationToken: stoppingToken);

                // Dead Letter Queue — Dzień 43.
                await consumerChannel.QueueDeclareAsync(
                    queue: RabbitMqTopology.DeadLetterQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await consumerChannel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: 1,
                    global: false,
                    cancellationToken: stoppingToken);

                var consumer =
                    new AsyncEventingBasicConsumer(consumerChannel);

                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    await ProcessDeliveryAsync(
                        consumerChannel,
                        publisherChannel,
                        delivery,
                        stoppingToken);
                };

                await consumerChannel.BasicConsumeAsync(
                    queue: RabbitMqTopology.Queue,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "Worker nasłuchuje kolejki {Queue}. " +
                    "Maksymalnie {Attempts} próby przetwarzania.",
                    RabbitMqTopology.Queue,
                    MaxRetries + 1);

                while (connection.IsOpen &&
                       consumerChannel.IsOpen &&
                       publisherChannel.IsOpen &&
                       !stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(2),
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Błąd połączenia RabbitMQ. " +
                    "Ponawiam połączenie za 5 sekund.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessDeliveryAsync(
        IChannel consumerChannel,
        IChannel publisherChannel,
        BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        byte[] body = delivery.Body.ToArray();

        try
        {
            ProductionOrderEvent orderEvent;
            int retryCount = 0;

            try
            {
                retryCount = ReadRetryCount(
                    delivery.BasicProperties.Headers);

                orderEvent = JsonSerializer
                    .Deserialize<ProductionOrderEvent>(body)
                    ?? throw new JsonException(
                        "Wiadomość jest pusta.");

                ValidateEvent(orderEvent);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Nieprawidłowa wiadomość. " +
                    "Przenoszę bezpośrednio do DLQ.");

                await ForwardAndAckAsync(
                    consumerChannel,
                    publisherChannel,
                    delivery,
                    body,
                    RabbitMqTopology.DeadLetterQueue,
                    retryCount,
                    "invalid-message",
                    cancellationToken);

                return;
            }

            // Symulacje błędów z Dnia 43.
            bool simulatedFailure =
                orderEvent.EventType == "DemoPermanentFailure" ||
                (
                    orderEvent.EventType == "DemoTransientFailure" &&
                    retryCount < 2
                );

            if (simulatedFailure)
            {
                if (retryCount < MaxRetries)
                {
                    int nextRetryCount = retryCount + 1;

                    _logger.LogWarning(
                        "Symulowany błąd: {EventType}. " +
                        "EventId={EventId}. " +
                        "Kolejna próba {Attempt}/{MaxAttempts}.",
                        orderEvent.EventType,
                        orderEvent.EventId,
                        nextRetryCount + 1,
                        MaxRetries + 1);

                    await ForwardAndAckAsync(
                        consumerChannel,
                        publisherChannel,
                        delivery,
                        body,
                        RabbitMqTopology.RetryQueue,
                        nextRetryCount,
                        "simulated-processing-failure",
                        cancellationToken);
                }
                else
                {
                    _logger.LogError(
                        "Wyczerpano próby przetworzenia. " +
                        "EventId={EventId}. Wysyłam do DLQ.",
                        orderEvent.EventId);

                    await ForwardAndAckAsync(
                        consumerChannel,
                        publisherChannel,
                        delivery,
                        body,
                        RabbitMqTopology.DeadLetterQueue,
                        retryCount,
                        "max-retries-exceeded",
                        cancellationToken);
                }

                return;
            }

            // NOWE: cały zapis biznesowy odbywa się
            // w OrderEventProcessor.
            bool isNew = await _processor.ProcessAsync(
                orderEvent,
                cancellationToken);

            if (isNew)
            {
                _logger.LogInformation(
                    "Przetworzono zdarzenie {EventType}. " +
                    "EventId={EventId}, OrderId={OrderId}.",
                    orderEvent.EventType,
                    orderEvent.EventId,
                    orderEvent.OrderId);
            }
            else
            {
                _logger.LogWarning(
                    "Pominięto duplikat wiadomości. " +
                    "EventId={EventId}.",
                    orderEvent.EventId);
            }

            // ACK dopiero po COMMIT w PostgreSQL.
            await consumerChannel.BasicAckAsync(
                deliveryTag: delivery.DeliveryTag,
                multiple: false,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Aplikacja kończy działanie.
            // Niepotwierdzona wiadomość może wrócić
            // do kolejki po zamknięciu połączenia.
        }
        catch (KeyNotFoundException exception)
        {
            // Zdarzenie odnosi się do nieistniejącego zlecenia.
            _logger.LogError(
                exception,
                "Nie znaleziono zlecenia. " +
                "Wysyłam wiadomość do DLQ.");

            try
            {
                await ForwardAndAckAsync(
                    consumerChannel,
                    publisherChannel,
                    delivery,
                    body,
                    RabbitMqTopology.DeadLetterQueue,
                    0,
                    "order-not-found",
                    cancellationToken);
            }
            catch (Exception transferException)
            {
                _logger.LogError(
                    transferException,
                    "Nie udało się przekazać wiadomości do DLQ.");

                await TryNackAsync(
                    consumerChannel,
                    delivery.DeliveryTag,
                    cancellationToken);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Błąd przetwarzania wiadomości. " +
                "Ponowienie po 5 sekundach.");

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await TryNackAsync(
                consumerChannel,
                delivery.DeliveryTag,
                cancellationToken);
        }
    }

    private async Task TryNackAsync(
        IChannel channel,
        ulong deliveryTag,
        CancellationToken cancellationToken)
    {
        try
        {
            await channel.BasicNackAsync(
                deliveryTag: deliveryTag,
                multiple: false,
                requeue: true,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Nie udało się wysłać NACK.");
        }
    }

    private static void ValidateEvent(
        ProductionOrderEvent orderEvent)
    {
        if (orderEvent.EventId == Guid.Empty ||
            orderEvent.OrderId == Guid.Empty ||
            string.IsNullOrWhiteSpace(orderEvent.EventType) ||
            orderEvent.EventType.Length > 100 ||
            orderEvent.OccurredAtUtc == default)
        {
            throw new JsonException(
                "Zdarzenie nie zawiera wymaganych danych.");
        }

        // Przyjmujemy tylko typy obsługiwane w ProductionFlow.
        bool knownType = orderEvent.EventType is
            "OrderCreated" or
            "OrderUpdated" or
            "OrderStatusChanged" or
            "ExecutionReportAdded" or
            "DemoOrderChanged" or
            "DemoTransientFailure" or
            "DemoPermanentFailure";

        if (!knownType)
        {
            throw new JsonException(
                "Nieobsługiwany typ zdarzenia.");
        }
    }

    private static int ReadRetryCount(
        IDictionary<string, object?>? headers)
    {
        const string headerName = "x-pf-retry-count";

        if (headers is null ||
            !headers.TryGetValue(headerName, out var value))
        {
            return 0;
        }

        int count = value switch
        {
            int number => number,
            byte number => number,
            short number => number,
            long number when number >= 0 &&
                             number <= MaxRetries => (int)number,
            _ => -1
        };

        if (count < 0 || count > MaxRetries)
        {
            throw new JsonException(
                "Niepoprawny licznik ponowień.");
        }

        return count;
    }

    private async Task ForwardAndAckAsync(
        IChannel consumerChannel,
        IChannel publisherChannel,
        BasicDeliverEventArgs delivery,
        byte[] body,
        string destinationQueue,
        int retryCount,
        string errorKind,
        CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            Persistent = true,

            ContentType =
                delivery.BasicProperties.ContentType ??
                "application/json",

            MessageId =
                delivery.BasicProperties.MessageId,

            Headers = new Dictionary<string, object?>
            {
                ["x-pf-retry-count"] = retryCount,
                ["x-pf-error-kind"] = errorKind
            }
        };

        using var publishCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        publishCts.CancelAfter(PublishTimeout);

        await publisherChannel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: destinationQueue,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: publishCts.Token);

        // ACK po potwierdzeniu przekazania wiadomości.
        await consumerChannel.BasicAckAsync(
            deliveryTag: delivery.DeliveryTag,
            multiple: false,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Przekazano wiadomość do {Queue}. " +
            "RetryCount={RetryCount}. Powód={Reason}.",
            destinationQueue,
            retryCount,
            errorKind);
    }
}