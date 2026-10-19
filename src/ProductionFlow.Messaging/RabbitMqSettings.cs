namespace ProductionFlow.Messaging;

public sealed class RabbitMqSettings
{
    public string Host { get; private init; } = "localhost";
    public int Port { get; private init; } = 5672;
    public string UserName { get; private init; } = "";
    public string Password { get; private init; } = "";

    public static RabbitMqSettings FromEnvironment()
    {
        string? userName = Environment.GetEnvironmentVariable(
            "RabbitMq__UserName");
        string? password = Environment.GetEnvironmentVariable(
            "RabbitMq__Password");
        string? portText = Environment.GetEnvironmentVariable(
            "RabbitMq__Port");

        if (string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Brak RabbitMq__UserName lub RabbitMq__Password.");
        }

        int port = 5672;
        if (!string.IsNullOrWhiteSpace(portText) &&
            (!int.TryParse(portText, out port) || port < 1 || port > 65535))
        {
            throw new InvalidOperationException(
                "RabbitMq__Port musi być poprawnym numerem portu.");
        }

        return new RabbitMqSettings
        {
            Host = Environment.GetEnvironmentVariable("RabbitMq__Host")
                ?? "localhost",
            Port = port,
            UserName = userName,
            Password = password
        };
    }
}