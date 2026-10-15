
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
        "Brak connection stringa ProductionFlow. Sprawdź User Secrets.");

// Kontekst danych produkcyjnych.
builder.Services.AddDbContext<ProductionFlowDbContext>(
    options => options.UseNpgsql(connectionString));

// Kontekst użytkowników i ról Identity.
builder.Services.AddDbContext<ProductionFlowIdentityDbContext>(
    options => options.UseNpgsql(
        connectionString,
        npgsql => npgsql.MigrationsHistoryTable(
            "__EFIdentityMigrationsHistory")));

// Konfiguracja ASP.NET Core Identity.
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;

        // Wymagania dotyczące haseł.
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        // Blokada konta po błędnych próbach.
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

    // REST API zwraca statusy HTTP zamiast przekierowań.
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

// Centralna polityka uprawnień z Dnia 18.
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

// Routing wymagany przed limitowaniem endpointów.
app.UseRouting();

// Sprawdzanie limitów żądań.
app.UseRateLimiter();

// Uwierzytelnianie i autoryzacja.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Konta demonstracyjne tylko w Development.
if (app.Environment.IsDevelopment())
{
    await IdentityDataSeeder.SeedDevelopmentAsync(
        app.Services,
        app.Configuration);
}

app.Run();
