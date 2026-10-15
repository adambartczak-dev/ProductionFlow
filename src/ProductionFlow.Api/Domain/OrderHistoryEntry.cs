
namespace ProductionFlow.Api.Domain;

public sealed class OrderHistoryEntry
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ProductionOrderId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string Details { get; private set; } = string.Empty;

    public string? Actor { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }
        = DateTimeOffset.UtcNow;

    // Konstruktor wykorzystywany przez EF Core.
    private OrderHistoryEntry()
    {
    }

    public OrderHistoryEntry(
        Guid productionOrderId,
        string eventType,
        string details,
        string? actor = null)
    {
        if (productionOrderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Id zlecenia jest wymagane.",
                nameof(productionOrderId));
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException(
                "Typ zdarzenia jest wymagany.",
                nameof(eventType));
        }

        if (string.IsNullOrWhiteSpace(details))
        {
            throw new ArgumentException(
                "Opis zdarzenia jest wymagany.",
                nameof(details));
        }

        ProductionOrderId = productionOrderId;
        EventType = eventType.Trim();
        Details = details.Trim();
        Actor = actor?.Trim();
    }
}
