namespace ProductionFlow.Api.Dtos;

public sealed record AssignWorkCenterRequest(
    Guid WorkCenterId);

public sealed record OperatorAssignmentResponse(
    Guid Id,
    string Email,
    string DisplayName,
    Guid? AssignedWorkCenterId);