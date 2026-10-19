using System.Text.Json;
using ProductionFlow.Messaging;

namespace ProductionFlow.Api.Data;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    // Konstruktor dla Entity Framework Core.
    private OutboxMessage()
    {
    }

    public static OutboxMessage Create(
        Guid orderId,
        string eventType,
        DateTimeOffset occurredAtUtc)
    {
        var orderEvent = new ProductionOrderEvent(
            EventId: Guid.NewGuid(),
            OrderId: orderId,
            EventType: eventType,
            OccurredAtUtc: occurredAtUtc);

        return new OutboxMessage
        {
            Id = orderEvent.EventId,
            OrderId = orderEvent.OrderId,
            EventType = orderEvent.EventType,
            Payload = JsonSerializer.Serialize(orderEvent),
            OccurredAtUtc = orderEvent.OccurredAtUtc
        };
    }

    public void MarkAsPublished()
    {
        PublishedAtUtc = DateTimeOffset.UtcNow;
    }
}