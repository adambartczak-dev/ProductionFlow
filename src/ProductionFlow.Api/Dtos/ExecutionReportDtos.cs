
using System.ComponentModel.DataAnnotations;

namespace ProductionFlow.Api.Dtos;

// Dane przesyłane przy dodawaniu raportu.
public sealed class CreateExecutionReportRequest
{
    [Required]
    [StringLength(100)]
    public string? OperatorId { get; init; }

    [Range(0, int.MaxValue)]
    public int GoodQuantity { get; init; }

    [Range(0, int.MaxValue)]
    public int ScrapQuantity { get; init; }
}

// Dane pojedynczego zapisanego raportu.
public sealed record ExecutionReportResponse(
    Guid Id,
    Guid ProductionOrderId,
    string OperatorId,
    int GoodQuantity,
    int ScrapQuantity,
    long TotalQuantity,
    DateTimeOffset CreatedAtUtc);

// Podsumowanie postępu zlecenia.
public sealed record ProductionProgressResponse(
    Guid Id,
    string Number,
    string Status,
    int PlannedQuantity,
    int GoodQuantity,
    int ScrapQuantity,
    int TotalQuantity,
    int RemainingQuantity,
    decimal ProgressPercent,
    int ReportsCount);
