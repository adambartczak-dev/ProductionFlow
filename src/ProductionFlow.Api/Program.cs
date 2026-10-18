
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ProductionFlow.Api.Authorization;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Identity;
using ProductionFlow.Api.Learning;

var builder = WebApplication.CreateBuilder(args);

// REST API i globalna ochrona CSRF.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(
        new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<LearningOrderService>();

// Połączenie z PostgreSQL.
var connectionString = builder.Configuration
    .GetConnectionString("ProductionFlow")
    ?? throw new InvalidOperationException(
        "Brak connection stringa ProductionFlow. " +
        "Sprawdź konfigurację środowiska.");

// Kontekst danych produkcyjnych.
builder.Services.AddDbContext<ProductionFlowDbContext>(
    options => options.UseNpgsql(connectionString));

// Kontekst użytkowników i ról Identity.
builder.Services.AddDbContext<ProductionFlowIdentityDbContext>(
    options => options.UseNpgsql(
        connectionString,
        npgsql => npgsql.MigrationsHistoryTable(
            "__EFIdentityMigrationsHistory")));

// ASP.NET Core Identity.
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ProductionFlowIdentityDbContext>()
    .AddDefaultTokenProviders();

// Zabezpieczenia cookie uwierzytelniającego.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "ProductionFlow.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;

    options.Cookie.SecurePolicy =
        builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;

    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode =
            StatusCodes.Status401Unauthorized;

        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode =
            StatusCodes.Status403Forbidden;

        return Task.CompletedTask;
    };
});

// Tokeny przeciw CSRF.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";

    options.Cookie.Name = "ProductionFlow.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;

    options.Cookie.SecurePolicy =
        builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
});

// Ograniczenie liczby żądań logowania.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                httpContext.Connection.RemoteIpAddress?
                    .ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// Polityka uprawnień.
var accessPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .AddRequirements(new ProductionAccessRequirement())
    .Build();

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = accessPolicy;
    options.FallbackPolicy = accessPolicy;
});

builder.Services.AddScoped<
    IAuthorizationHandler,
    ProductionAccessHandler>();

var app = builder.Build();

// Centralna obsługa błędów.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Endpoint wykorzystywany przez Docker healthcheck.
app.MapGet("/health", async (
    ProductionFlowDbContext db,
    CancellationToken cancellationToken) =>
{
    bool databaseAvailable =
        await db.Database.CanConnectAsync(cancellationToken);

    return databaseAvailable
        ? Results.Ok(new { status = "ok" })
        : Results.StatusCode(
            StatusCodes.Status503ServiceUnavailable);
})
.AllowAnonymous();

// Automatyczne przygotowanie lokalnego środowiska Docker.
if (app.Environment.IsDevelopment())
{
    bool applyMigrations = app.Configuration.GetValue<bool>(
        "Database:ApplyMigrationsOnStartup");

    if (applyMigrations)
    {
        await using var scope =
            app.Services.CreateAsyncScope();

        // Migracje głównej bazy produkcyjnej.
        var productionDb = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        await productionDb.Database.MigrateAsync();

        // Migracje ASP.NET Core Identity.
        var identityDb = scope.ServiceProvider
            .GetRequiredService<ProductionFlowIdentityDbContext>();

        await identityDb.Database.MigrateAsync();

        // Przykładowe katalogi w pustej bazie.
        await DevelopmentCatalogSeeder.SeedAsync(productionDb);
    }

    // Konta demonstracyjne tylko w Development.
    await IdentityDataSeeder.SeedDevelopmentAsync(
        app.Services,
        app.Configuration);
}

app.Run();
