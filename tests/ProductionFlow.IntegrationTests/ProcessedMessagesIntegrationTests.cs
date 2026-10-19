using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductionFlow.Api.Data;
using Xunit;

namespace ProductionFlow.IntegrationTests;

public sealed class ProcessedMessagesIntegrationTests
    : IClassFixture<ProductionFlowApiFixture>
{
    private readonly ProductionFlowApiFixture _fixture;

    public ProcessedMessagesIntegrationTests(
        ProductionFlowApiFixture fixture)
    {
        _fixture = fixture;
    }

    // TEST 1:
    // Ten sam EventId można zarejestrować tylko raz.
    [Fact]
    public async Task DuplicateEventId_IsRegisteredOnlyOnce()
    {
        await using var scope =
            _fixture.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        Guid eventId = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();

        int firstResult = await InsertAsync(
            db,
            eventId,
            orderId);

        int secondResult = await InsertAsync(
            db,
            eventId,
            orderId);

        Assert.Equal(1, firstResult);
        Assert.Equal(0, secondResult);

        int count = await db.ProcessedMessages
            .CountAsync(item => item.EventId == eventId);

        Assert.Equal(1, count);
    }

    // TEST 2:
    // Wycofany zapis nie oznacza wiadomości jako obsłużonej.
    [Fact]
    public async Task Rollback_DoesNotRegisterMessage()
    {
        Guid eventId = Guid.NewGuid();
        Guid orderId = Guid.NewGuid();

        await using (var scope =
            _fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ProductionFlowDbContext>();

            await using var transaction =
                await db.Database.BeginTransactionAsync();

            int result = await InsertAsync(
                db,
                eventId,
                orderId);

            Assert.Equal(1, result);

            await transaction.RollbackAsync();
        }

        await using var verificationScope =
            _fixture.Services.CreateAsyncScope();

        var verificationDb = verificationScope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        bool exists = await verificationDb.ProcessedMessages
            .AnyAsync(item => item.EventId == eventId);

        Assert.False(exists);
    }

    private static Task<int> InsertAsync(
        ProductionFlowDbContext db,
        Guid eventId,
        Guid orderId)
    {
        string eventType = "OrderCreated";
        DateTimeOffset processedAtUtc =
            DateTimeOffset.UtcNow;

        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ProcessedMessages"
                ("EventId", "OrderId", "EventType", "ProcessedAtUtc")
            VALUES
                ({eventId}, {orderId}, {eventType}, {processedAtUtc})
            ON CONFLICT ("EventId") DO NOTHING;
            """);
    }
}