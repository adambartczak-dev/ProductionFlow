using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Domain;
using ProductionFlow.Messaging;
using ProductionFlow.Worker;
using Xunit;

namespace ProductionFlow.IntegrationTests;

public sealed class OrderEventReliabilityIntegrationTests
    : IClassFixture<ProductionFlowApiFixture>
{
    private readonly ProductionFlowApiFixture _fixture;

    public OrderEventReliabilityIntegrationTests(
        ProductionFlowApiFixture fixture)
    {
        _fixture = fixture;
    }

    // TEST 1:
    // Kilka równoczesnych dostarczeń tego samego EventId.
    [Fact]
    public async Task ConcurrentDuplicates_AreProcessedOnlyOnce()
    {
        Guid orderId = await CreateTestOrderAsync();

        await using var scope =
            _fixture.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        string connectionString =
            db.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "Brak connection stringa testowej bazy.");

        await using var dataSource =
            NpgsqlDataSource.Create(connectionString);

        var processor = new OrderEventProcessor(dataSource);

        var orderEvent = new ProductionOrderEvent(
            Guid.NewGuid(),
            orderId,
            "ExecutionReportAdded",
            DateTimeOffset.UtcNow);

        // Symulujemy 5 jednoczesnych dostarczeń.
        Task<bool>[] tasks = Enumerable.Range(0, 5)
            .Select(_ => processor.ProcessAsync(orderEvent))
            .ToArray();

        bool[] results = await Task.WhenAll(tasks);

        // Tylko jedno wywołanie jest nowe.
        Assert.Equal(1, results.Count(result => result));
        Assert.Equal(4, results.Count(result => !result));

        int processedCount = await db.ProcessedMessages
            .CountAsync(item =>
                item.EventId == orderEvent.EventId);

        Assert.Equal(1, processedCount);

        var summary = await db.OrderProductionSummaries
            .AsNoTracking()
            .SingleAsync(item => item.OrderId == orderId);

        Assert.Equal(40, summary.GoodQuantity);
        Assert.Equal(5, summary.ScrapQuantity);
        Assert.Equal(1, summary.ReportsCount);
    }

    // TEST 2:
    // Dwa różne EventId dotyczące tego samego zlecenia.
    [Fact]
    public async Task ConcurrentEvents_KeepSummaryConsistent()
    {
        Guid orderId = await CreateTestOrderAsync();

        await using var scope =
            _fixture.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        string connectionString =
            db.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "Brak connection stringa testowej bazy.");

        await using var dataSource =
            NpgsqlDataSource.Create(connectionString);

        var processor = new OrderEventProcessor(dataSource);

        var firstEvent = new ProductionOrderEvent(
            Guid.NewGuid(),
            orderId,
            "OrderStatusChanged",
            DateTimeOffset.UtcNow);

        var secondEvent = new ProductionOrderEvent(
            Guid.NewGuid(),
            orderId,
            "ExecutionReportAdded",
            DateTimeOffset.UtcNow);

        // Dwie niezależne wiadomości równocześnie.
        bool[] results = await Task.WhenAll(
            processor.ProcessAsync(firstEvent),
            processor.ProcessAsync(secondEvent));

        Assert.All(results, Assert.True);

        // Dwa różne EventId powinny zostać zarejestrowane.
        int processedCount = await db.ProcessedMessages
            .CountAsync(item =>
                item.OrderId == orderId);

        Assert.Equal(2, processedCount);

        // Nie sumujemy dwukrotnie tych samych raportów.
        var summary = await db.OrderProductionSummaries
            .AsNoTracking()
            .SingleAsync(item => item.OrderId == orderId);

        Assert.Equal(100, summary.PlannedQuantity);
        Assert.Equal(40, summary.GoodQuantity);
        Assert.Equal(5, summary.ScrapQuantity);
        Assert.Equal(1, summary.ReportsCount);

        Assert.Equal(
            ProductionOrderStatus.InProgress,
            summary.Status);
    }

    // Pomocnicze przygotowanie zlecenia testowego.
    private async Task<Guid> CreateTestOrderAsync()
    {
        await using var scope =
            _fixture.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        var product = new Product(
            $"PRD-{Guid.NewGuid():N}",
            "Produkt niezawodność");

        var workCenter = new WorkCenter(
            $"WC-{Guid.NewGuid():N}",
            "Stanowisko niezawodność");

        db.Products.Add(product);
        db.WorkCenters.Add(workCenter);

        await db.SaveChangesAsync();

        var order = new ProductionOrder(
            $"ORD-{Guid.NewGuid():N}",
            product.Id,
            workCenter.Id,
            100,
            DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(7)));

        db.ProductionOrders.Add(order);

        await db.SaveChangesAsync();

        order.Release();
        await db.SaveChangesAsync();

        order.Start();
        await db.SaveChangesAsync();

        order.AddReport(
            "operator-test",
            goodQuantity: 40,
            scrapQuantity: 5);

        await db.SaveChangesAsync();

        return order.Id;
    }
}