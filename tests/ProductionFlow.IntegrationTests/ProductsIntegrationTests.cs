
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Dtos;
using ProductionFlow.Api.Identity;
using Xunit;

namespace ProductionFlow.IntegrationTests;

public sealed class ProductsIntegrationTests
    : IClassFixture<ProductionFlowApiFixture>
{
    private readonly ProductionFlowApiFixture _fixture;

    public ProductsIntegrationTests(
        ProductionFlowApiFixture fixture)
    {
        _fixture = fixture;
    }

    // TEST 1
    // Użytkownik niezalogowany nie może pobrać produktów.
    [Fact]
    public async Task GetProducts_WithoutLogin_Returns401()
    {
        using var client = _fixture.CreateClient();

        using var response = await client.GetAsync(
            "/api/products");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    // TEST 2
    // Administrator może się zalogować i odczytać swoje dane.
    [Fact]
    public async Task Login_AsAdmin_ReturnsCurrentUser()
    {
        using var client = await LoginAsync(
            "admin@productionflow.local");

        var user = await client.GetFromJsonAsync<CurrentUserResponse>(
            "/api/auth/me");

        Assert.NotNull(user);

        Assert.Equal(
            "admin@productionflow.local",
            user.Email);

        Assert.Equal(
            "Administrator Test",
            user.DisplayName);

        Assert.Contains(
            ProductionFlowRoles.Admin,
            user.Roles);
    }

    // TEST 3
    // Utworzenie produktu zapisuje dane w prawdziwym PostgreSQL.
    [Fact]
    public async Task CreateProduct_AsAdmin_PersistsInPostgreSql()
    {
        using var client = await LoginAsync(
            "admin@productionflow.local");

        // Po zalogowaniu pobieramy nowy token CSRF.
        string csrf = await GetCsrfTokenAsync(client);

        string code = "PF-T20-" +
            Guid.NewGuid().ToString("N")[..12];

        var productRequest = new ProductRequest
        {
            Code = code,
            Name = "Produkt integracyjny"
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/products");

        request.Headers.Add("X-CSRF-TOKEN", csrf);

        request.Content = JsonContent.Create(productRequest);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var created = await response.Content
            .ReadFromJsonAsync<ProductResponse>();

        Assert.NotNull(created);
        Assert.Equal(code, created.Code);

        // Niezależnie sprawdzamy rzeczywisty zapis w bazie.
        await using var scope = _fixture.Services
            .CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        var stored = await db.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(
                product => product.Id == created.Id);

        Assert.NotNull(stored);
        Assert.Equal(code, stored.Code);
        Assert.Equal("Produkt integracyjny", stored.Name);
    }

    // TEST 4
    // Poprawnie zalogowany administrator nie może wysłać
    // POST bez tokenu CSRF.
    [Fact]
    public async Task CreateProduct_WithoutCsrf_Returns400()
    {
        using var client = await LoginAsync(
            "admin@productionflow.local");

        var product = new ProductRequest
        {
            Code = "PF-T20-" +
                Guid.NewGuid().ToString("N")[..12],
            Name = "Brak tokenu CSRF"
        };

        using var response = await client.PostAsJsonAsync(
            "/api/products",
            product);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    // TEST 5
    // Operator nie może pobrać ogólnej listy zleceń.
    [Fact]
    public async Task GetAllOrders_AsOperator_Returns403()
    {
        using var client = await LoginAsync(
            "operator@productionflow.local");

        using var response = await client.GetAsync(
            "/api/orders");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    // Metoda pomocnicza do logowania kont testowych.
    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = _fixture.CreateClient();

        string csrf = await GetCsrfTokenAsync(client);

        var loginData = new LoginRequest
        {
            Email = email,
            Password = ProductionFlowApiFixture.TestPassword
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/login");

        request.Headers.Add("X-CSRF-TOKEN", csrf);

        request.Content = JsonContent.Create(loginData);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        return client;
    }

    // Pobranie tokenu i zapisanie cookie przez HttpClient.
    private static async Task<string> GetCsrfTokenAsync(
        HttpClient client)
    {
        var result = await client.GetFromJsonAsync<CsrfResponse>(
            "/api/auth/csrf-token");

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(
            result.RequestToken));

        return result.RequestToken;
    }

    private sealed record CsrfResponse(
        string RequestToken);
}
