using Npgsql;
using ProductionFlow.Messaging;

namespace ProductionFlow.Worker;

public sealed class OrderEventProcessor
{
    private readonly NpgsqlDataSource _dataSource;

    public OrderEventProcessor(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<bool> ProcessAsync(
        ProductionOrderEvent orderEvent,
        CancellationToken cancellationToken = default)
    {
        bool isDemo = orderEvent.EventType.StartsWith(
            "Demo",
            StringComparison.Ordinal);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        // Jedna transakcja dla rejestracji EventId
        // oraz aktualizacji podsumowania.
        await using var transaction =
            await connection.BeginTransactionAsync(
                cancellationToken);

        if (!isDemo)
        {
            // Blokujemy wiersz zlecenia.
            // Chroni to przed równoczesną zmianą danych
            // przez API lub innego konsumenta.
            const string lockSql = """
                SELECT "Id"
                FROM "ProductionOrders"
                WHERE "Id" = @orderId
                FOR UPDATE;
                """;

            await using var lockCommand =
                new NpgsqlCommand(
                    lockSql,
                    connection,
                    transaction);

            lockCommand.Parameters.AddWithValue(
                "orderId",
                orderEvent.OrderId);

            object? orderId = await lockCommand
                .ExecuteScalarAsync(cancellationToken);

            if (orderId is null)
            {
                throw new KeyNotFoundException(
                    $"Nie znaleziono zlecenia {orderEvent.OrderId}.");
            }
        }

        // Próba rejestracji EventId.
        const string registerSql = """
            INSERT INTO "ProcessedMessages"
                ("EventId", "OrderId", "EventType", "ProcessedAtUtc")
            VALUES
                (@eventId, @orderId, @eventType, @processedAtUtc)
            ON CONFLICT ("EventId") DO NOTHING;
            """;

        await using var registerCommand =
            new NpgsqlCommand(
                registerSql,
                connection,
                transaction);

        registerCommand.Parameters.AddWithValue(
            "eventId",
            orderEvent.EventId);

        registerCommand.Parameters.AddWithValue(
            "orderId",
            orderEvent.OrderId);

        registerCommand.Parameters.AddWithValue(
            "eventType",
            orderEvent.EventType);

        registerCommand.Parameters.AddWithValue(
            "processedAtUtc",
            DateTimeOffset.UtcNow);

        int registeredRows = await registerCommand
            .ExecuteNonQueryAsync(cancellationToken);

        if (registeredRows == 0)
        {
            // Duplikat — nie przeliczamy podsumowania.
            await transaction.CommitAsync(cancellationToken);

            return false;
        }

        if (!isDemo)
        {
            // Podsumowanie obliczamy ze źródłowych danych,
            // a nie przez dodawanie wartości do starej sumy.
            const string summarySql = """
                INSERT INTO "OrderProductionSummaries"
                    (
                        "OrderId",
                        "Number",
                        "Status",
                        "PlannedQuantity",
                        "GoodQuantity",
                        "ScrapQuantity",
                        "ReportsCount",
                        "UpdatedAtUtc"
                    )
                SELECT
                    o."Id",
                    o."Number",
                    o."Status",
                    o."PlannedQuantity",
                    COALESCE(SUM(r."GoodQuantity"), 0)::integer,
                    COALESCE(SUM(r."ScrapQuantity"), 0)::integer,
                    COUNT(r."Id")::integer,
                    @updatedAtUtc
                FROM "ProductionOrders" o
                LEFT JOIN "ExecutionReports" r
                    ON r."ProductionOrderId" = o."Id"
                WHERE o."Id" = @orderId
                GROUP BY
                    o."Id",
                    o."Number",
                    o."Status",
                    o."PlannedQuantity"
                ON CONFLICT ("OrderId")
                DO UPDATE SET
                    "Number" = EXCLUDED."Number",
                    "Status" = EXCLUDED."Status",
                    "PlannedQuantity" = EXCLUDED."PlannedQuantity",
                    "GoodQuantity" = EXCLUDED."GoodQuantity",
                    "ScrapQuantity" = EXCLUDED."ScrapQuantity",
                    "ReportsCount" = EXCLUDED."ReportsCount",
                    "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc";
                """;

            await using var summaryCommand =
                new NpgsqlCommand(
                    summarySql,
                    connection,
                    transaction);

            summaryCommand.Parameters.AddWithValue(
                "orderId",
                orderEvent.OrderId);

            summaryCommand.Parameters.AddWithValue(
                "updatedAtUtc",
                DateTimeOffset.UtcNow);

            int affectedRows = await summaryCommand
                .ExecuteNonQueryAsync(cancellationToken);

            if (affectedRows != 1)
            {
                throw new InvalidOperationException(
                    "Nie udało się zaktualizować " +
                    "podsumowania zlecenia.");
            }
        }

        // COMMIT obu zapisów.
        await transaction.CommitAsync(cancellationToken);

        return true;
    }
}