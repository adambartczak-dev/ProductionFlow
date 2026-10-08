namespace ProductionFlow.Api.Learning;

public sealed record LearningOrderDto(
    int Id,
    string Number,
    string? Notes);