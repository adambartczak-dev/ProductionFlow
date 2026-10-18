
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ProductionFlow.Api.Authorization;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Diagnostics;
using ProductionFlow.Api.Identity;
using ProductionFlow.Api.Learning;
using ProductionFlow.Api.Realtime;
using Serilog;
using Serilog.Events;

// Konfiguracja Serilog z Dnia 36.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override(
        "Microsoft",
        LogEventLevel.Warning)
    .MinimumLevel.Override(
        "Microsoft.Hosting.Lifetime",
        LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] " +
            "{Message:lj} | RequestId={RequestId}" +
            "{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// REST API i ochrona CSRF.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(
        new AutoValidateAntiforgeryTokenAttribute());

    // NOWE: powiadomienia po operacjach na zleceniach.
    options.Filters.AddService<
        ProductionOrderNotificationFilter>();
});

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<LearningOrderService>();

// NOWE: serwer SignalR.
builder.Services.AddSignalR();

// NOWE: filtr powiadomień.
builder.Services.AddScoped<
    ProductionOrderNotificationFilter>();

// Połączenie z PostgreSQL.
var connectionString = builder.Configuration
    .GetConnectionString("ProductionFlow")
    ?? throw new InvalidOperationException(
        "Brak connection stringa ProductionFlow. " +
        "Sprawdź konfigurację środowiska.");

builder.Services.AddDbContext<ProductionFlowDbContext>(
    options => options.UseNpgsql(connectionString));

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

// Cookie uwierzytelniające.
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

// Ochrona CSRF dla operacji HTTP.
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

// Ograniczenie prób logowania.
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

// Istniejąca polityka uprawnień biznesowych.
var accessPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .AddRequirements(new ProductionAccessRequirement())
    .Build();

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = accessPolicy;
    options.FallbackPolicy = accessPolicy;

    // NOWE: osobna polityka dla huba SignalR.
    options.AddPolicy("ProductionRealtime", policy =>
    {
        policy.RequireAuthenticatedUser();

        policy.RequireRole(
            ProductionFlowRoles.Admin,
            ProductionFlowRoles.Planner,
            ProductionFlowRoles.Operator);
    });
});

builder.Services.AddScoped<
    IAuthorizationHandler,
    ProductionAccessHandler>();

var app = builder.Build();

// Request ID z Dnia 36.
app.UseMiddleware<RequestIdMiddleware>();

// Logowanie żądań HTTP przez Serilog.
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} " +
        "-> {StatusCode} w {Elapsed:0.0000} ms";

    options.EnrichDiagnosticContext =
        (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set(
                "RequestId",
                httpContext.TraceIdentifier);
        };

    options.IncludeQueryInRequestPath = false;

    options.GetLevel = (httpContext, elapsed, exception) =>
    {
        if (
            exception is not null ||
            httpContext.Response.StatusCode >= 500
        )
        {
            return LogEventLevel.Error;
        }

        if (httpContext.Response.StatusCode == 409)
        {
            return LogEventLevel.Warning;
        }

        return LogEventLevel.Information;
    };
});

// Obsługa błędów.
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

// Dotychczasowe endpointy REST API.
app.MapControllers();

// NOWE: endpoint komunikacji czasu rzeczywistego.
app.MapHub<ProductionHub>("/hubs/production");

// Sprawdzenie działania procesu API.
app.MapGet("/health/live", () =>
{
    return Results.Ok(new
    {
        status = "alive"
    });
})
.AllowAnonymous();

// Sprawdzenie połączenia API z PostgreSQL.
app.MapGet("/health", async (
    ProductionFlowDbContext db,
    CancellationToken cancellationToken) =>
{
    bool databaseAvailable;

    try
    {
        databaseAvailable =
            await db.Database.CanConnectAsync(
                cancellationToken);
    }
    catch (OperationCanceledException)
        when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception)
    {
        databaseAvailable = false;
    }

    if (!databaseAvailable)
    {
        return Results.StatusCode(
            StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new
    {
        status = "ok",
        database = "connected"
    });
})
.AllowAnonymous();

// Przygotowanie lokalnej bazy i kont demonstracyjnych.
if (app.Environment.IsDevelopment())
{
    bool applyMigrations = app.Configuration.GetValue<bool>(
        "Database:ApplyMigrationsOnStartup");

    if (applyMigrations)
    {
        await using var scope =
            app.Services.CreateAsyncScope();

        var productionDb = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        await productionDb.Database.MigrateAsync();

        var identityDb = scope.ServiceProvider
            .GetRequiredService<ProductionFlowIdentityDbContext>();

        await identityDb.Database.MigrateAsync();

        await DevelopmentCatalogSeeder.SeedAsync(
            productionDb);
    }

    await IdentityDataSeeder.SeedDevelopmentAsync(
        app.Services,
        app.Configuration);
}

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
