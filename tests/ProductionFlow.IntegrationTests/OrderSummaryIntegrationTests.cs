using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Domain;
using ProductionFlow.Messaging;
using ProductionFlow.Worker;
using Xunit;

namespace ProductionFlow.IntegrationTests;

public sealed class OrderSummaryIntegrationTests
    : IClassFixture<ProductionFlowApiFixture>
{
    private readonly ProductionFlowApiFixture _fixture;

    public OrderSummaryIntegrationTests(
        ProductionFlowApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Reports_UpdateSummary_WithoutCountingDuplicate()
    {
        await using var scope =
            _fixture.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        var product = new Product(
            $"PRD-{Guid.NewGuid():N}",
            "Produkt testowy");

        var workCenter = new WorkCenter(
            $"WC-{Guid.NewGuid():N}",
            "Stanowisko testowe");

        db.Products.Add(product);
        db.WorkCenters.Add(workCenter);

        await db.SaveChangesAsync();

        var order = new ProductionOrder(
            $"ORD-{Guid.NewGuid():N}",
            product.Id,
            workCenter.Id,
            100,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));

        db.ProductionOrders.Add(order);

        await db.SaveChangesAsync();

        order.Release();
        order.Start();

        await db.SaveChangesAsync();

        order.AddReport(
            "operator-test",
            goodQuantity: 40,
            scrapQuantity: 5);

        await db.SaveChangesAsync();

        // POPRAWKA: pobieramy oryginalny connection string EF Core.
        string connectionString =
            db.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "Brak connection stringa testowej bazy.");

        await using var dataSource =
            NpgsqlDataSource.Create(connectionString);

        var processor = new OrderEventProcessor(dataSource);

        var firstEvent = new ProductionOrderEvent(
            Guid.NewGuid(),
            order.Id,
            "ExecutionReportAdded",
            DateTimeOffset.UtcNow);

        // Pierwsze dostarczenie.
        bool firstResult = await processor.ProcessAsync(firstEvent);

        // Ponowne dostarczenie tego samego EventId.
        bool duplicateResult = await processor.ProcessAsync(firstEvent);

        Assert.True(firstResult);
        Assert.False(duplicateResult);

        var firstSummary = await db.OrderProductionSummaries
            .AsNoTracking()
            .SingleAsync(item => item.OrderId == order.Id);

        Assert.Equal(40, firstSummary.GoodQuantity);
        Assert.Equal(5, firstSummary.ScrapQuantity);
        Assert.Equal(1, firstSummary.ReportsCount);

        // Drugi raport.
        order.AddReport(
            "operator-test",
            goodQuantity: 30,
            scrapQuantity: 5);

        await db.SaveChangesAsync();

        var secondEvent = new ProductionOrderEvent(
            Guid.NewGuid(),
            order.Id,
            "ExecutionReportAdded",
            DateTimeOffset.UtcNow);

        bool secondResult =
            await processor.ProcessAsync(secondEvent);

        Assert.True(secondResult);

        var finalSummary = await db.OrderProductionSummaries
            .AsNoTracking()
            .SingleAsync(item => item.OrderId == order.Id);

        Assert.Equal(70, finalSummary.GoodQuantity);
        Assert.Equal(10, finalSummary.ScrapQuantity);
        Assert.Equal(2, finalSummary.ReportsCount);
        Assert.Equal(100, finalSummary.PlannedQuantity);
        Assert.Equal(
            ProductionOrderStatus.InProgress,
            finalSummary.Status);

        int processedCount = await db.ProcessedMessages
            .CountAsync(item => item.OrderId == order.Id);

        Assert.Equal(2, processedCount);
    }

    [Fact]
    public async Task MissingOrder_DoesNotRegisterEvent()
    {
        await using var scope =
            _fixture.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        // POPRAWKA: pobieramy oryginalny connection string EF Core.
        string connectionString =
            db.Database.GetConnectionString()
            ?? throw new InvalidOperationException(
                "Brak connection stringa testowej bazy.");

        await using var dataSource =
            NpgsqlDataSource.Create(connectionString);

        var processor = new OrderEventProcessor(dataSource);

        var orderEvent = new ProductionOrderEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "OrderCreated",
            DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => processor.ProcessAsync(orderEvent));

        bool exists = await db.ProcessedMessages
            .AnyAsync(item =>
                item.EventId == orderEvent.EventId);

        Assert.False(exists);
    }
}