using ProductionFlow.Api.Domain;
using Xunit;

namespace ProductionFlow.UnitTests;

public sealed class ProductionOrderQuantityTests
{
    [Fact]
    public void FullProductionCycle_AccountsForGoodAndScrap_AndRequiresExplicitCompletion()
    {
        var order = CreateOrder();

        Assert.Equal(ProductionOrderStatus.Draft, order.Status);

        order.Release();
        Assert.Equal(ProductionOrderStatus.Released, order.Status);

        order.Start();
        Assert.Equal(ProductionOrderStatus.InProgress, order.Status);

        var firstReport = order.AddReport("operator-1", 57, 3);

        Assert.Equal(order.Id, firstReport.ProductionOrderId);
        Assert.Equal(60, order.TotalQuantity);
        Assert.Equal(40, order.RemainingQuantity);
        Assert.Equal(60m, order.ProgressPercent);
        Assert.Single(order.ExecutionReports);

        order.AddReport("operator-1", 38, 2);

        Assert.Equal(95, order.GoodQuantity);
        Assert.Equal(5, order.ScrapQuantity);
        Assert.Equal(100, order.TotalQuantity);
        Assert.Equal(0, order.RemainingQuantity);
        Assert.Equal(100m, order.ProgressPercent);
        Assert.Equal(2, order.ExecutionReports.Count);

        Assert.Equal(ProductionOrderStatus.InProgress, order.Status);

        order.Complete();

        Assert.Equal(ProductionOrderStatus.Completed, order.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(99)]
    public void Complete_WhenPlanIsNotFullyAccountedFor_ThrowsWithoutChangingOrder(
        int reportedQuantity)
    {
        var order = CreateStartedOrder();

        if (reportedQuantity > 0)
        {
            order.AddReport("operator-1", reportedQuantity, 0);
        }

        int reportsBefore = order.ExecutionReports.Count;

        Assert.Throws<InvalidOperationException>(() => order.Complete());

        Assert.Equal(ProductionOrderStatus.InProgress, order.Status);
        Assert.Equal(reportedQuantity, order.TotalQuantity);
        Assert.Equal(reportsBefore, order.ExecutionReports.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenPlannedQuantityIsNotPositive_Throws(
        int plannedQuantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateOrder(plannedQuantity));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(-1, 10)]
    [InlineData(10, -1)]
    public void AddReport_WhenAnyQuantityIsNegative_ThrowsWithoutAddingReport(
        int goodQuantity,
        int scrapQuantity)
    {
        var order = CreateStartedOrder();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => order.AddReport("operator-1", goodQuantity, scrapQuantity));

        Assert.Empty(order.ExecutionReports);
        Assert.Equal(0, order.GoodQuantity);
        Assert.Equal(0, order.ScrapQuantity);
        Assert.Equal(0, order.TotalQuantity);
        Assert.Equal(ProductionOrderStatus.InProgress, order.Status);
    }

    [Fact]
    public void AddReport_WhenBothQuantitiesAreZero_ThrowsWithoutAddingReport()
    {
        var order = CreateStartedOrder();

        Assert.Throws<ArgumentException>(
            () => order.AddReport("operator-1", 0, 0));

        Assert.Empty(order.ExecutionReports);
        Assert.Equal(0, order.TotalQuantity);
    }

    [Theory]
    [InlineData(41, 0)]
    [InlineData(0, 41)]
    [InlineData(25, 16)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void AddReport_WhenCombinedQuantityExceedsRemainingPlan_RejectsEntireReport(
        int goodQuantity,
        int scrapQuantity)
    {
        var order = CreateStartedOrder();
        order.AddReport("operator-1", 57, 3);

        Assert.Throws<InvalidOperationException>(
            () => order.AddReport("operator-1", goodQuantity, scrapQuantity));

        Assert.Single(order.ExecutionReports);
        Assert.Equal(57, order.GoodQuantity);
        Assert.Equal(3, order.ScrapQuantity);
        Assert.Equal(60, order.TotalQuantity);
        Assert.Equal(40, order.RemainingQuantity);
        Assert.Equal(60m, order.ProgressPercent);
        Assert.Equal(ProductionOrderStatus.InProgress, order.Status);
    }

    [Fact]
    public void EditDraft_WhenNewPlanIsInvalid_PreservesAllPreviousDetails()
    {
        var order = CreateOrder();

        string numberBefore = order.Number;
        Guid productBefore = order.ProductId;
        Guid workCenterBefore = order.WorkCenterId;
        int planBefore = order.PlannedQuantity;
        DateOnly dueDateBefore = order.DueDate;

        Assert.Throws<ArgumentOutOfRangeException>(() => order.EditDraft(
            "PF-CHANGED",
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            new DateOnly(2030, 2, 1)));

        Assert.Equal(numberBefore, order.Number);
        Assert.Equal(productBefore, order.ProductId);
        Assert.Equal(workCenterBefore, order.WorkCenterId);
        Assert.Equal(planBefore, order.PlannedQuantity);
        Assert.Equal(dueDateBefore, order.DueDate);
        Assert.Equal(ProductionOrderStatus.Draft, order.Status);
    }

    private static ProductionOrder CreateOrder(int plannedQuantity = 100)
    {
        return new ProductionOrder(
            "PF-0001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            plannedQuantity,
            new DateOnly(2030, 1, 1));
    }

    private static ProductionOrder CreateStartedOrder()
    {
        var order = CreateOrder();
        order.Release();
        order.Start();

        return order;
    }
}