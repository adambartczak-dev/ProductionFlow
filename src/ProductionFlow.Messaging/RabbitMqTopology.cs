namespace ProductionFlow.Messaging;

public static class RabbitMqTopology
{
    // Istniejąca konfiguracja z Dnia 39.
    public const string Exchange = "productionflow.orders";

    public const string Queue = "productionflow.orders.worker";

    public const string RoutingKey = "orders.changed";

    // NOWE: kolejka opóźnionych ponowień.
    public const string RetryQueue = "productionflow.orders.retry";

    // NOWE: kolejka trwale nieobsłużonych wiadomości.
    public const string DeadLetterQueue =
        "productionflow.orders.dead-letter";
}