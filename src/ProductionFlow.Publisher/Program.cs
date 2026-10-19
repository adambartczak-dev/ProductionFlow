using System.Text;
using System.Text.Json;
using ProductionFlow.Messaging;
using RabbitMQ.Client;

// Domyślne zdarzenie demonstracyjne.
string eventType = args.Length == 0
    ? "DemoOrderChanged"
    : args[0];

string[] allowedTypes =
[
    "DemoOrderChanged",
    "DemoTransientFailure",
    "DemoPermanentFailure",
    "DemoInvalidJson"
];

if (!allowedTypes.Contains(eventType))
{
    throw new ArgumentException(
        $"Nieobsługiwany typ wiadomości: {eventType}");
}

var settings = RabbitMqSettings.FromEnvironment();

var factory = new ConnectionFactory
{
    HostName = settings.Host,
    Port = settings.Port,
    UserName = settings.UserName,
    Password = settings.Password
};

await using var connection =
    await factory.CreateConnectionAsync();

// Potwierdzenia publikacji.
var channelOptions = new CreateChannelOptions(
    publisherConfirmationsEnabled: true,
    publisherConfirmationTrackingEnabled: true);

await using var channel =
    await connection.CreateChannelAsync(channelOptions);

// Istniejący Exchange.
await channel.ExchangeDeclareAsync(
    exchange: RabbitMqTopology.Exchange,
    type: ExchangeType.Direct,
    durable: true,
    autoDelete: false);

// Istniejąca kolejka.
await channel.QueueDeclareAsync(
    queue: RabbitMqTopology.Queue,
    durable: true,
    exclusive: false,
    autoDelete: false);

await channel.QueueBindAsync(
    queue: RabbitMqTopology.Queue,
    exchange: RabbitMqTopology.Exchange,
    routingKey: RabbitMqTopology.RoutingKey);

// Nowe identyfikatory dla każdej wiadomości testowej.
Guid eventId = Guid.NewGuid();
Guid orderId = Guid.NewGuid();

// Utworzenie zdarzenia.
var orderEvent = new ProductionOrderEvent(
    EventId: eventId,
    OrderId: orderId,
    EventType: eventType,
    OccurredAtUtc: DateTimeOffset.UtcNow);

// Normalnie wysyłamy poprawny JSON.
// Jeden scenariusz celowo wysyła błędny JSON.
byte[] body = eventType == "DemoInvalidJson"
    ? Encoding.UTF8.GetBytes("{ invalid-json")
    : JsonSerializer.SerializeToUtf8Bytes(orderEvent);

var properties = new BasicProperties
{
    Persistent = true,
    ContentType = "application/json",
    MessageId = eventId.ToString()
};

// Wysłanie wiadomości do głównego Exchange.
await channel.BasicPublishAsync(
    exchange: RabbitMqTopology.Exchange,
    routingKey: RabbitMqTopology.RoutingKey,
    mandatory: true,
    basicProperties: properties,
    body: body);

Console.WriteLine(
    $"Wysłano: {eventType}, " +
    $"EventId={eventId}, OrderId={orderId}");