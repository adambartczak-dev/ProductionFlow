namespace ProductionFlow.Api.Domain;

public sealed class ProductionOrder
{
    private readonly List<ExecutionReport> _executionReports = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Number { get; private set; } = string.Empty;
    public Guid ProductId { get; private set; }
    public Guid WorkCenterId { get; private set; }
    public int PlannedQuantity { get; private set; }
    public DateOnly DueDate { get; private set; }

    public ProductionOrderStatus Status { get; private set; }
        = ProductionOrderStatus.Draft;

    public IReadOnlyCollection<ExecutionReport> ExecutionReports
        => _executionReports.AsReadOnly();

    public int GoodQuantity
        => _executionReports.Sum(report => report.GoodQuantity);

    public int ScrapQuantity
        => _executionReports.Sum(report => report.ScrapQuantity);

    public int TotalQuantity => GoodQuantity + ScrapQuantity;
    public int RemainingQuantity => PlannedQuantity - TotalQuantity;

    public decimal ProgressPercent
        => Math.Round(100m * TotalQuantity / PlannedQuantity, 2);

    public ProductionOrder(
        string number,
        Guid productId,
        Guid workCenterId,
        int plannedQuantity,
        DateOnly dueDate)
    {
        SetDetails(number, productId, workCenterId, plannedQuantity, dueDate);
    }

    public void EditDraft(
        string number,
        Guid productId,
        Guid workCenterId,
        int plannedQuantity,
        DateOnly dueDate)
    {
        EnsureStatus(ProductionOrderStatus.Draft);
        SetDetails(number, productId, workCenterId, plannedQuantity, dueDate);
    }

    public void Release()
    {
        EnsureStatus(ProductionOrderStatus.Draft);
        Status = ProductionOrderStatus.Released;
    }

    public void Start()
    {
        EnsureStatus(ProductionOrderStatus.Released);
        Status = ProductionOrderStatus.InProgress;
    }

    public void Cancel()
    {
        if (Status != ProductionOrderStatus.Draft
            && Status != ProductionOrderStatus.Released)
        {
            throw new InvalidOperationException(
                "Anulować można tylko zlecenie Draft lub Released.");
        }

        Status = ProductionOrderStatus.Cancelled;
    }

    public ExecutionReport AddReport(
        string operatorId,
        int goodQuantity,
        int scrapQuantity)
    {
        EnsureStatus(ProductionOrderStatus.InProgress);

        var report = new ExecutionReport(
            Id, operatorId, goodQuantity, scrapQuantity);

        if (report.TotalQuantity > RemainingQuantity)
        {
            throw new InvalidOperationException(
                "Raport przekracza pozostałą ilość do wykonania.");
        }

        // Dodajemy dopiero po sprawdzeniu wszystkich reguł.
        _executionReports.Add(report);
        return report;
    }

    public void Complete()
    {
        EnsureStatus(ProductionOrderStatus.InProgress);

        if (TotalQuantity != PlannedQuantity)
        {
            throw new InvalidOperationException(
                "Przed zakończeniem trzeba rozliczyć całą planowaną ilość.");
        }

        Status = ProductionOrderStatus.Completed;
    }

    private void EnsureStatus(ProductionOrderStatus expectedStatus)
    {
        if (Status != expectedStatus)
        {
            throw new InvalidOperationException(
                $"Operacja wymaga statusu {expectedStatus}. Aktualny status: {Status}.");
        }
    }

    private void SetDetails(
        string number,
        Guid productId,
        Guid workCenterId,
        int plannedQuantity,
        DateOnly dueDate)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new ArgumentException(
                "Numer zlecenia jest wymagany.", nameof(number));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException(
                "Produkt jest wymagany.", nameof(productId));
        }

        if (workCenterId == Guid.Empty)
        {
            throw new ArgumentException(
                "Stanowisko jest wymagane.", nameof(workCenterId));
        }

        if (plannedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(plannedQuantity), "Planowana ilość musi być dodatnia.");
        }

        if (dueDate == default)
        {
            throw new ArgumentException(
                "Termin zlecenia jest wymagany.", nameof(dueDate));
        }

        // Wszystkie dane sprawdzamy przed pierwszą zmianą właściwości.
        Number = number.Trim();
        ProductId = productId;
        WorkCenterId = workCenterId;
        PlannedQuantity = plannedQuantity;
        DueDate = dueDate;
    }
}