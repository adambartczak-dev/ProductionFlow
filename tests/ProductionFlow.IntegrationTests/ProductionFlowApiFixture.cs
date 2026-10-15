
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Identity;
using Testcontainers.PostgreSql;
using Xunit;

namespace ProductionFlow.IntegrationTests;

public sealed class ProductionFlowApiFixture : IAsyncLifetime
{
    // Hasło wyłącznie do jednorazowych kont testowych.
    public const string TestPassword = "Test-Only#2026-Password";

    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:18")
            .WithDatabase("productionflow_tests")
            .WithUsername("testuser")
            .WithPassword("Temporary-Postgres#2026")
            .Build();

    private TestApiFactory? _factory;

    public IServiceProvider Services =>
        _factory?.Services
        ?? throw new InvalidOperationException(
            "Środowisko testowe nie zostało uruchomione.");

    public HttpClient CreateClient()
    {
        if (_factory is null)
        {
            throw new InvalidOperationException(
                "Środowisko testowe nie zostało uruchomione.");
        }

        return _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true,
                AllowAutoRedirect = false
            });
    }

    public async Task InitializeAsync()
    {
        // 1. Uruchomienie testowego PostgreSQL.
        await _postgres.StartAsync();

        // 2. Uruchomienie API z testowym connection stringiem.
        _factory = new TestApiFactory(
            _postgres.GetConnectionString());

        using var scope = Services.CreateScope();

        // 3. Migracje danych produkcyjnych.
        var productionDb = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        await productionDb.Database.MigrateAsync();

        // 4. Migracje Identity.
        var identityDb = scope.ServiceProvider
            .GetRequiredService<ProductionFlowIdentityDbContext>();

        await identityDb.Database.MigrateAsync();

        // 5. Tworzenie kont testowych.
        await SeedUsersAsync(scope.ServiceProvider);
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();

        await _postgres.DisposeAsync();
    }

    private static async Task SeedUsersAsync(
        IServiceProvider services)
    {
        var roleManager = services
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var userManager = services
            .GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var roleName in ProductionFlowRoles.All)
        {
            var role = new IdentityRole<Guid>(roleName)
            {
                Id = Guid.NewGuid()
            };

            var result = await roleManager.CreateAsync(role);

            EnsureSucceeded(result);
        }

        await CreateUserAsync(
            userManager,
            "admin@productionflow.local",
            "Administrator Test",
            ProductionFlowRoles.Admin);

        await CreateUserAsync(
            userManager,
            "operator@productionflow.local",
            "Operator Test",
            ProductionFlowRoles.Operator);
    }

    private static async Task CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string displayName,
        string role)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName
        };

        var createResult = await userManager.CreateAsync(
            user,
            TestPassword);

        EnsureSucceeded(createResult);

        var roleResult = await userManager.AddToRoleAsync(
            user,
            role);

        EnsureSucceeded(roleResult);
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        string errors = string.Join(
            "; ",
            result.Errors.Select(error => error.Description));

        throw new InvalidOperationException(
            $"Błąd przygotowania kont testowych: {errors}");
    }

    private sealed class TestApiFactory
        : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;

        public TestApiFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            // Środowisko testowe bez seedera Development.
            builder.UseEnvironment("Testing");

            // NOWE: ustawienie connection stringa przed
            // odczytaniem go przez Program.cs.
            builder.UseSetting(
                "ConnectionStrings:ProductionFlow",
                _connectionString);

            // Folder aplikacji ProductionFlow.Api.
            builder.UseContentRoot(
                FindApiProjectDirectory());
        }

        private static string FindApiProjectDirectory()
        {
            DirectoryInfo? directory = new(
                AppContext.BaseDirectory);

            while (directory is not null)
            {
                string solutionFile = Path.Combine(
                    directory.FullName,
                    "ProductionFlow.sln");

                if (File.Exists(solutionFile))
                {
                    return Path.Combine(
                        directory.FullName,
                        "src",
                        "ProductionFlow.Api");
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "Nie znaleziono folderu rozwiązania ProductionFlow.");
        }
    }
}
