namespace ProductionFlow.Api.Data;

public sealed class ProcessedMessage
{
    public Guid EventId { get; private set; }

    public Guid OrderId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public DateTimeOffset ProcessedAtUtc { get; private set; }

    // Konstruktor dla Entity Framework Core.
    private ProcessedMessage()
    {
    }

    public ProcessedMessage(
        Guid eventId,
        Guid orderId,
        string eventType)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException(
                "EventId nie może być pusty.",
                nameof(eventId));
        }

        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "OrderId nie może być pusty.",
                nameof(orderId));
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException(
                "Typ zdarzenia jest wymagany.",
                nameof(eventType));
        }

        EventId = eventId;
        OrderId = orderId;
        EventType = eventType;
        ProcessedAtUtc = DateTimeOffset.UtcNow;
    }
}