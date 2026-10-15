
namespace ProductionFlow.Api.Dtos;

public sealed record OrderHistoryResponse(
    Guid Id,
    Guid ProductionOrderId,
    string EventType,
    string Details,
    string? Actor,
    DateTimeOffset OccurredAtUtc);
