using ProductionFlow.Api.Domain;

namespace ProductionFlow.Api.Data;

public sealed class OrderProductionSummary
{
    public Guid OrderId { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public ProductionOrderStatus Status { get; private set; }

    public int PlannedQuantity { get; private set; }

    public int GoodQuantity { get; private set; }

    public int ScrapQuantity { get; private set; }

    public int ReportsCount { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    // Konstruktor wykorzystywany przez EF Core.
    private OrderProductionSummary()
    {
    }
}