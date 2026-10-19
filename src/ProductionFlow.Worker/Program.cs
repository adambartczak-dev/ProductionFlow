using Npgsql;
using ProductionFlow.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("ProductionFlow")
    ?? throw new InvalidOperationException(
        "Brak ConnectionStrings:ProductionFlow.");

builder.Services.AddSingleton(
    NpgsqlDataSource.Create(connectionString));

// NOWE: procesor zdarzeń biznesowych.
builder.Services.AddSingleton<OrderEventProcessor>();

builder.Services.AddHostedService<OrderEventsWorker>();

await builder.Build().RunAsync();