using ProductionFlow.Api.Domain;
using Xunit;

namespace ProductionFlow.UnitTests;

public sealed class ProductionOrderStatusTests
{
    [Theory]
    [InlineData(ProductionOrderStatus.Released)]
    [InlineData(ProductionOrderStatus.InProgress)]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Cancelled)]
    public void Release_WhenStatusIsNotDraft_ThrowsAndPreservesStatus(
        ProductionOrderStatus initialStatus)
    {
        var order = CreateOrderInStatus(initialStatus);

        Assert.Throws<InvalidOperationException>(() => order.Release());

        Assert.Equal(initialStatus, order.Status);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Draft)]
    [InlineData(ProductionOrderStatus.InProgress)]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Cancelled)]
    public void Start_WhenStatusIsNotReleased_ThrowsAndPreservesStatus(
        ProductionOrderStatus initialStatus)
    {
        var order = CreateOrderInStatus(initialStatus);

        Assert.Throws<InvalidOperationException>(() => order.Start());

        Assert.Equal(initialStatus, order.Status);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Draft)]
    [InlineData(ProductionOrderStatus.Released)]
    public void Cancel_BeforeProductionStarts_ChangesStatusToCancelled(
        ProductionOrderStatus initialStatus)
    {
        var order = CreateOrderInStatus(initialStatus);

        order.Cancel();

        Assert.Equal(ProductionOrderStatus.Cancelled, order.Status);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.InProgress)]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Cancelled)]
    public void Cancel_WhenStatusIsNotDraftOrReleased_ThrowsAndPreservesStatus(
        ProductionOrderStatus initialStatus)
    {
        var order = CreateOrderInStatus(initialStatus);

        Assert.Throws<InvalidOperationException>(() => order.Cancel());

        Assert.Equal(initialStatus, order.Status);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Draft)]
    [InlineData(ProductionOrderStatus.Released)]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Cancelled)]
    public void Complete_WhenStatusIsNotInProgress_ThrowsAndPreservesStatus(
        ProductionOrderStatus initialStatus)
    {
        var order = CreateOrderInStatus(initialStatus);

        Assert.Throws<InvalidOperationException>(() => order.Complete());

        Assert.Equal(initialStatus, order.Status);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Draft)]
    [InlineData(ProductionOrderStatus.Released)]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Cancelled)]
    public void AddReport_WhenStatusIsNotInProgress_ThrowsWithoutAddingReport(
        ProductionOrderStatus initialStatus)
    {
        var order = CreateOrderInStatus(initialStatus);
        int reportsBefore = order.ExecutionReports.Count;
        int quantityBefore = order.TotalQuantity;

        Assert.Throws<InvalidOperationException>(
            () => order.AddReport("operator-1", 10, 0));

        Assert.Equal(initialStatus, order.Status);
        Assert.Equal(reportsBefore, order.ExecutionReports.Count);
        Assert.Equal(quantityBefore, order.TotalQuantity);
    }

    [Theory]
    [InlineData(ProductionOrderStatus.Released)]
    [InlineData(ProductionOrderStatus.InProgress)]
    [InlineData(ProductionOrderStatus.Completed)]
    [InlineData(ProductionOrderStatus.Cancelled)]
    public void EditDraft_WhenStatusIsNotDraft_ThrowsWithoutChangingDetails(
        ProductionOrderStatus initialStatus)
    {
        var order = CreateOrderInStatus(initialStatus);

        string numberBefore = order.Number;
        Guid productBefore = order.ProductId;
        Guid workCenterBefore = order.WorkCenterId;
        int planBefore = order.PlannedQuantity;
        DateOnly dueDateBefore = order.DueDate;

        Assert.Throws<InvalidOperationException>(() => order.EditDraft(
            "PF-CHANGED",
            Guid.NewGuid(),
            Guid.NewGuid(),
            250,
            new DateOnly(2030, 2, 1)));

        Assert.Equal(initialStatus, order.Status);
        Assert.Equal(numberBefore, order.Number);
        Assert.Equal(productBefore, order.ProductId);
        Assert.Equal(workCenterBefore, order.WorkCenterId);
        Assert.Equal(planBefore, order.PlannedQuantity);
        Assert.Equal(dueDateBefore, order.DueDate);
    }

    private static ProductionOrder CreateOrderInStatus(
        ProductionOrderStatus status)
    {
        var order = new ProductionOrder(
            "PF-0001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            100,
            new DateOnly(2030, 1, 1));

        switch (status)
        {
            case ProductionOrderStatus.Draft:
                break;

            case ProductionOrderStatus.Released:
                order.Release();
                break;

            case ProductionOrderStatus.InProgress:
                order.Release();
                order.Start();
                break;

            case ProductionOrderStatus.Completed:
                order.Release();
                order.Start();
                order.AddReport("operator-1", 95, 5);
                order.Complete();
                break;

            case ProductionOrderStatus.Cancelled:
                order.Cancel();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        return order;
    }
}