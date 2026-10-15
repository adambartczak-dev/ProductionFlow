using System.ComponentModel.DataAnnotations;

namespace ProductionFlow.Api.Dtos;

public sealed class WorkCenterRequest
{
    [Required]
    [StringLength(50)]
    public string? Code { get; init; }

    [Required]
    [StringLength(100)]
    public string? Name { get; init; }
}

public sealed record WorkCenterResponse(
    Guid Id,
    string Code,
    string Name);
