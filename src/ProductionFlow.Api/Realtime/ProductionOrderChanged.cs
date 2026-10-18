
namespace ProductionFlow.Api.Realtime;

public sealed record ProductionOrderChanged(
    Guid OrderId,
    Guid WorkCenterId,
    string ChangeType,
    string Status,
    DateTimeOffset OccurredAtUtc);
