using ProductionFlow.Api.Learning;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped<LearningOrderService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Na tym etapie uruchamiamy ćwiczenia lokalnie przez HTTP.
app.UseAuthorization();
app.MapControllers();

app.Run();