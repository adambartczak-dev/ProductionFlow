using Microsoft.EntityFrameworkCore;
using ProductionFlow.Api.Data;
using ProductionFlow.OutboxPublisher;

var builder = Host.CreateApplicationBuilder(args);

// Połączenie z tą samą bazą PostgreSQL,
// z której korzysta ProductionFlow.Api.
var connectionString = builder.Configuration
    .GetConnectionString("ProductionFlow")
    ?? throw new InvalidOperationException(
        "Brak connection stringa ProductionFlow.");

// Rejestracja istniejącego DbContext.
builder.Services.AddDbContext<ProductionFlowDbContext>(
    options => options.UseNpgsql(connectionString));

// Rejestracja nowej usługi działającej w tle.
builder.Services.AddHostedService<OutboxPublisherService>();

await builder.Build().RunAsync();