
using System.ComponentModel.DataAnnotations;

namespace ProductionFlow.Api.Dtos;

// Dane wymagane przy tworzeniu zlecenia.
public sealed class CreateProductionOrderRequest
{
    [Required]
    [StringLength(50)]
    public string? Number { get; init; }

    public Guid ProductId { get; init; }

    public Guid WorkCenterId { get; init; }

    [Range(1, int.MaxValue)]
    public int PlannedQuantity { get; init; }

    public DateOnly DueDate { get; init; }
}

// Dane zwracane dla pojedynczego zlecenia.
public sealed record ProductionOrderResponse(
    Guid Id,
    string Number,
    Guid ProductId,
    Guid WorkCenterId,
    int PlannedQuantity,
    DateOnly DueDate,
    string Status);

// Parametry filtrowania i paginacji listy.
public sealed class ProductionOrderListQuery
{
    public string? Search { get; init; }

    public string? Status { get; init; }

    public Guid? ProductId { get; init; }

    public Guid? WorkCenterId { get; init; }

    public string SortBy { get; init; } = "dueDate";

    public string SortDirection { get; init; } = "asc";

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 10;
}

// Uniwersalna odpowiedź zawierająca stronę wyników.
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
