namespace ProductionFlow.Messaging;

public sealed record ProductionOrderEvent(
    Guid EventId,
    Guid OrderId,
    string EventType,
    DateTimeOffset OccurredAtUtc);