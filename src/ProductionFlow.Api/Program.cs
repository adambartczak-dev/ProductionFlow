
using Microsoft.EntityFrameworkCore;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Learning;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<LearningOrderService>();

var connectionString = builder.Configuration
    .GetConnectionString("ProductionFlow")
    ?? throw new InvalidOperationException(
        "Brak connection stringa ProductionFlow. Sprawdź User Secrets.");

builder.Services.AddDbContext<ProductionFlowDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// Centralna obsługa nieprzewidzianych wyjątków.
app.UseExceptionHandler();

// Standardowe odpowiedzi dla błędów bez treści.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
