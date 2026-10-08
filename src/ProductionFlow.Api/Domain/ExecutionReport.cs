namespace ProductionFlow.Api.Domain;

public sealed class ExecutionReport
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ProductionOrderId { get; private set; }
    public string OperatorId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public int GoodQuantity { get; private set; }
    public int ScrapQuantity { get; private set; }

    public long TotalQuantity => (long)GoodQuantity + ScrapQuantity;

    internal ExecutionReport(
        Guid productionOrderId,
        string operatorId,
        int goodQuantity,
        int scrapQuantity)
    {
        if (productionOrderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Id zlecenia jest wymagane.", nameof(productionOrderId));
        }

        if (string.IsNullOrWhiteSpace(operatorId))
        {
            throw new ArgumentException(
                "Autor raportu jest wymagany.", nameof(operatorId));
        }

        if (goodQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(goodQuantity), "Liczba dobrych sztuk nie może być ujemna.");
        }

        if (scrapQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scrapQuantity), "Liczba braków nie może być ujemna.");
        }

        if ((long)goodQuantity + scrapQuantity == 0)
        {
            throw new ArgumentException(
                "Raport musi zawierać co najmniej jedną sztukę.");
        }

        ProductionOrderId = productionOrderId;
        OperatorId = operatorId.Trim();
        GoodQuantity = goodQuantity;
        ScrapQuantity = scrapQuantity;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }
}