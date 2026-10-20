using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Domain;
using ProductionFlow.Api.Dtos;
using Xunit;

namespace ProductionFlow.IntegrationTests;

public sealed class OrderHistoryIntegrationTests
    : IClassFixture<ProductionFlowApiFixture>
{
    private readonly ProductionFlowApiFixture _fixture;

    public OrderHistoryIntegrationTests(
        ProductionFlowApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExistingOrder_WithHistory_Returns200AndEntries()
    {
        Guid orderId = await CreateOrderAsync(clearHistory: false);
        using var client = _fixture.CreateClient();
        await LoginAsync(client);

        var history = await client.GetFromJsonAsync<
            List<OrderHistoryResponse>>(
            $"/api/orders/{orderId}/history");

        Assert.NotNull(history);
        Assert.Single(history);
        Assert.Equal("OrderCreated", history[0].EventType);
    }

    [Fact]
    public async Task ExistingOrder_WithoutHistory_Returns200AndEmptyList()
    {
        Guid orderId = await CreateOrderAsync(clearHistory: true);
        using var client = _fixture.CreateClient();
        await LoginAsync(client);

        using var response = await client.GetAsync(
            $"/api/orders/{orderId}/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var history = await response.Content.ReadFromJsonAsync<
            List<OrderHistoryResponse>>();

        Assert.NotNull(history);
        Assert.Empty(history);
    }

    [Fact]
    public async Task MissingOrder_Returns404()
    {
        using var client = _fixture.CreateClient();
        await LoginAsync(client);

        using var response = await client.GetAsync(
            $"/api/orders/{Guid.NewGuid()}/history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Guid> CreateOrderAsync(bool clearHistory)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        string suffix = Guid.NewGuid().ToString("N");
        var product = new Product($"P47-{suffix}", "Produkt testowy");
        var center = new WorkCenter($"WC47-{suffix}", "Stanowisko testowe");

        db.Products.Add(product);
        db.WorkCenters.Add(center);

        var order = new ProductionOrder(
            $"O47-{suffix}",
            product.Id,
            center.Id,
            100,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)));

        db.ProductionOrders.Add(order);
        await db.SaveChangesAsync();

        if (clearHistory)
        {
            // W teście celowo tworzymy rzadki przypadek:
            // zlecenie istnieje, lecz nie ma wpisów historii.
            await db.OrderHistoryEntries
                .Where(entry => entry.ProductionOrderId == order.Id)
                .ExecuteDeleteAsync();
        }

        return order.Id;
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>(
            "/api/auth/csrf-token");

        Assert.NotNull(csrf);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/login");

        request.Headers.Add("X-CSRF-TOKEN", csrf.RequestToken);
        request.Content = JsonContent.Create(new LoginRequest
        {
            Email = "admin@productionflow.local",
            Password = ProductionFlowApiFixture.TestPassword
        });

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record CsrfResponse(string RequestToken);
}