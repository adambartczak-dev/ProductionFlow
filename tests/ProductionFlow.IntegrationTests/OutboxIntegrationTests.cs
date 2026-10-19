using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Domain;
using ProductionFlow.Messaging;
using Xunit;

namespace ProductionFlow.IntegrationTests;

public sealed class OutboxIntegrationTests
    : IClassFixture<ProductionFlowApiFixture>
{
    private readonly ProductionFlowApiFixture _fixture;

    public OutboxIntegrationTests(
        ProductionFlowApiFixture fixture)
    {
        _fixture = fixture;
    }

    // TEST 1:
    // Operacje biznesowe tworzą odpowiadające im wpisy Outbox.
    [Fact]
    public async Task OrderChanges_CreateOutboxMessages()
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
            10,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));

        db.ProductionOrders.Add(order);

        await db.SaveChangesAsync();

        // Edycja zlecenia Draft.
        order.EditDraft(
            order.Number,
            product.Id,
            workCenter.Id,
            12,
            order.DueDate);

        await db.SaveChangesAsync();

        // Zmiany statusów.
        order.Release();
        await db.SaveChangesAsync();

        order.Start();
        await db.SaveChangesAsync();

        // Raport produkcyjny.
        order.AddReport(
            "operator-testowy",
            goodQuantity: 3,
            scrapQuantity: 1);

        await db.SaveChangesAsync();

        var messages = await db.OutboxMessages
            .AsNoTracking()
            .Where(item => item.OrderId == order.Id)
            .ToListAsync();

        Assert.Equal(5, messages.Count);

        Assert.Contains(
            messages,
            item => item.EventType == "OrderCreated");

        Assert.Contains(
            messages,
            item => item.EventType == "OrderUpdated");

        Assert.Equal(
            2,
            messages.Count(item =>
                item.EventType == "OrderStatusChanged"));

        Assert.Contains(
            messages,
            item => item.EventType == "ExecutionReportAdded");

        Assert.All(
            messages,
            item => Assert.Null(item.PublishedAtUtc));

        // Sprawdzamy zgodność JSON z modelem Dnia 39.
        foreach (var message in messages)
        {
            var eventData = JsonSerializer
                .Deserialize<ProductionOrderEvent>(
                    message.Payload);

            Assert.NotNull(eventData);
            Assert.Equal(message.Id, eventData.EventId);
            Assert.Equal(order.Id, eventData.OrderId);
            Assert.Equal(message.EventType, eventData.EventType);
        }

        // Historia i Outbox zawierają tyle samo zdarzeń.
        int historyCount = await db.OrderHistoryEntries
            .CountAsync(item =>
                item.ProductionOrderId == order.Id);

        Assert.Equal(messages.Count, historyCount);
    }

    // TEST 2:
    // ROLLBACK usuwa zarówno zmianę biznesową,
    // jak i jej nieopublikowaną wiadomość.
    [Fact]
    public async Task Rollback_RemovesOrderAndOutboxMessage()
    {
        var orderId = Guid.Empty;

        await using (var scope =
            _fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ProductionFlowDbContext>();

            var product = new Product(
                $"PRD-{Guid.NewGuid():N}",
                "Produkt rollback");

            var workCenter = new WorkCenter(
                $"WC-{Guid.NewGuid():N}",
                "Stanowisko rollback");

            db.Products.Add(product);
            db.WorkCenters.Add(workCenter);

            await db.SaveChangesAsync();

            await using var transaction =
                await db.Database.BeginTransactionAsync();

            var order = new ProductionOrder(
                $"ORD-{Guid.NewGuid():N}",
                product.Id,
                workCenter.Id,
                10,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));

            orderId = order.Id;

            db.ProductionOrders.Add(order);

            await db.SaveChangesAsync();

            Assert.True(
                await db.OutboxMessages.AnyAsync(
                    item => item.OrderId == orderId));

            // Celowo wycofujemy całą transakcję.
            await transaction.RollbackAsync();
        }

        // Nowy DbContext, aby odczytać stan po ROLLBACK.
        await using var verificationScope =
            _fixture.Services.CreateAsyncScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        bool orderExists = await verificationDb
            .ProductionOrders
            .AnyAsync(item => item.Id == orderId);

        bool outboxExists = await verificationDb
            .OutboxMessages
            .AnyAsync(item => item.OrderId == orderId);

        bool historyExists = await verificationDb
            .OrderHistoryEntries
            .AnyAsync(item =>
                item.ProductionOrderId == orderId);

        Assert.False(orderExists);
        Assert.False(outboxExists);
        Assert.False(historyExists);
    }
}