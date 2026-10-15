
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Domain;
using ProductionFlow.Api.Dtos;
using ProductionFlow.Api.Identity;
using Xunit;

namespace ProductionFlow.IntegrationTests;

public sealed class OrdersSecurityIntegrationTests
    : IClassFixture<ProductionFlowApiFixture>
{
    private readonly ProductionFlowApiFixture _fixture;

    public OrdersSecurityIntegrationTests(
        ProductionFlowApiFixture fixture)
    {
        _fixture = fixture;
    }

    // TEST 1
    // Planowana ilość 0 jest nieprawidłowa.
    // API nie może utworzyć takiego zlecenia.
    [Fact]
    public async Task CreateOrder_InvalidQuantity_Returns400()
    {
        using var client = _fixture.CreateClient();

        string csrf = await LoginAsync(
            client,
            "admin@productionflow.local");

        var catalog = await CreateCatalogAsync();

        string number = NewOrderNumber();

        var request = new CreateProductionOrderRequest
        {
            Number = number,
            ProductId = catalog.ProductId,
            WorkCenterId = catalog.WorkCenterId,
            PlannedQuantity = 0,
            DueDate = DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(7))
        };

        using var response = await PostWithCsrfAsync(
            client,
            "/api/orders",
            csrf,
            request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        // Sprawdzamy, czy zlecenie nie zostało zapisane.
        await using var scope = _fixture.Services
            .CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        bool exists = await db.ProductionOrders
            .AsNoTracking()
            .AnyAsync(order => order.Number == number);

        Assert.False(exists);
    }

    // TEST 2
    // Operator nie ma uprawnień do tworzenia zleceń.
    [Fact]
    public async Task CreateOrder_AsOperator_Returns403()
    {
        using var client = _fixture.CreateClient();

        string csrf = await LoginAsync(
            client,
            "operator@productionflow.local");

        var catalog = await CreateCatalogAsync();
        string number = NewOrderNumber();

        var request = new CreateProductionOrderRequest
        {
            Number = number,
            ProductId = catalog.ProductId,
            WorkCenterId = catalog.WorkCenterId,
            PlannedQuantity = 100,
            DueDate = DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(7))
        };

        using var response = await PostWithCsrfAsync(
            client,
            "/api/orders",
            csrf,
            request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);

        await using var scope = _fixture.Services
            .CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        Assert.False(await db.ProductionOrders
            .AnyAsync(order => order.Number == number));
    }

    // TEST 3
    // Numer zlecenia musi być unikalny.
    [Fact]
    public async Task CreateOrder_DuplicateNumber_Returns409()
    {
        using var client = _fixture.CreateClient();

        string csrf = await LoginAsync(
            client,
            "admin@productionflow.local");

        var order = await CreateOrderAsync(client, csrf);

        var duplicate = new CreateProductionOrderRequest
        {
            Number = order.Number,
            ProductId = order.ProductId,
            WorkCenterId = order.WorkCenterId,
            PlannedQuantity = 100,
            DueDate = order.DueDate
        };

        using var response = await PostWithCsrfAsync(
            client,
            "/api/orders",
            csrf,
            duplicate);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        // W bazie musi pozostać tylko jedno zlecenie.
        await using var scope = _fixture.Services
            .CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        int count = await db.ProductionOrders
            .CountAsync(item => item.Number == order.Number);

        Assert.Equal(1, count);

        int historyCount = await db.OrderHistoryEntries
            .CountAsync(item =>
                item.ProductionOrderId == order.Id);

        Assert.Equal(1, historyCount);
    }

    // TEST 4
    // Nie można wykonać Start bez wcześniejszego Release.
    [Fact]
    public async Task Start_DraftOrder_Returns409()
    {
        using var client = _fixture.CreateClient();

        string csrf = await LoginAsync(
            client,
            "admin@productionflow.local");

        var order = await CreateOrderAsync(client, csrf);

        using var response = await PostWithCsrfAsync(
            client,
            $"/api/orders/{order.Id}/start",
            csrf);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        var current = await client
            .GetFromJsonAsync<ProductionOrderResponse>(
                $"/api/orders/{order.Id}");

        Assert.NotNull(current);
        Assert.Equal("Draft", current.Status);

        // Nieudana zmiana nie może tworzyć historii.
        var history = await client
            .GetFromJsonAsync<List<OrderHistoryResponse>>(
                $"/api/orders/{order.Id}/history");

        Assert.NotNull(history);
        Assert.Single(history);

        Assert.Equal(
            "OrderCreated",
            history[0].EventType);
    }

    // TEST 5
    // Raport przekraczający plan nie może zostać zapisany.
    [Fact]
    public async Task AddReport_OverPlannedQuantity_Returns409()
    {
        using var client = _fixture.CreateClient();

        string csrf = await LoginAsync(
            client,
            "admin@productionflow.local");

        var order = await CreateOrderAsync(client, csrf);

        await ReleaseAndStartAsync(
            client,
            order.Id,
            csrf);

        var report = new CreateExecutionReportRequest
        {
            OperatorId = "operator-test",
            GoodQuantity = 101,
            ScrapQuantity = 0
        };

        using var response = await PostWithCsrfAsync(
            client,
            $"/api/orders/{order.Id}/reports",
            csrf,
            report);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        var progress = await client
            .GetFromJsonAsync<ProductionProgressResponse>(
                $"/api/orders/{order.Id}/progress");

        Assert.NotNull(progress);

        Assert.Equal(0, progress.TotalQuantity);
        Assert.Equal(0, progress.ReportsCount);
        Assert.Equal(100, progress.RemainingQuantity);
        Assert.Equal("InProgress", progress.Status);

        // Create + Release + Start = 3 wpisy historii.
        var history = await client
            .GetFromJsonAsync<List<OrderHistoryResponse>>(
                $"/api/orders/{order.Id}/history");

        Assert.NotNull(history);
        Assert.Equal(3, history.Count);
    }

    // TEST 6
    // Dwa równoczesne raporty po 60 sztuk dla planu 100.
    // Tylko jeden raport może zostać zaakceptowany.
    [Fact]
    public async Task AddReports_Concurrently_PreventsOverproduction()
    {
        using var client = _fixture.CreateClient();

        string csrf = await LoginAsync(
            client,
            "admin@productionflow.local");

        var order = await CreateOrderAsync(client, csrf);

        await ReleaseAndStartAsync(
            client,
            order.Id,
            csrf);

        string url = $"/api/orders/{order.Id}/reports";

        var firstReport = new CreateExecutionReportRequest
        {
            OperatorId = "operator-A",
            GoodQuantity = 60,
            ScrapQuantity = 0
        };

        var secondReport = new CreateExecutionReportRequest
        {
            OperatorId = "operator-B",
            GoodQuantity = 60,
            ScrapQuantity = 0
        };

        // Oba żądania rozpoczynamy bez oczekiwania
        // na zakończenie pierwszego.
        Task<HttpResponseMessage> firstTask =
            PostWithCsrfAsync(
                client,
                url,
                csrf,
                firstReport);

        Task<HttpResponseMessage> secondTask =
            PostWithCsrfAsync(
                client,
                url,
                csrf,
                secondReport);

        var responses = await Task.WhenAll(
            firstTask,
            secondTask);

        try
        {
            var statuses = responses
                .Select(response => response.StatusCode)
                .ToList();

            Assert.Contains(
                HttpStatusCode.Created,
                statuses);

            Assert.Contains(
                HttpStatusCode.Conflict,
                statuses);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        var progress = await client
            .GetFromJsonAsync<ProductionProgressResponse>(
                $"/api/orders/{order.Id}/progress");

        Assert.NotNull(progress);

        Assert.Equal(60, progress.TotalQuantity);
        Assert.Equal(40, progress.RemainingQuantity);
        Assert.Equal(1, progress.ReportsCount);
        Assert.Equal("InProgress", progress.Status);

        // Potwierdzamy wynik bezpośrednio w PostgreSQL.
        await using var scope = _fixture.Services
            .CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        int reportsCount = await db.ExecutionReports
            .CountAsync(report =>
                report.ProductionOrderId == order.Id);

        Assert.Equal(1, reportsCount);

        // Create + Release + Start + jeden raport.
        int historyCount = await db.OrderHistoryEntries
            .CountAsync(entry =>
                entry.ProductionOrderId == order.Id);

        Assert.Equal(4, historyCount);
    }

    // TEST 7
    // Operator może odczytać zlecenie swojego stanowiska,
    // ale nie zlecenie przypisane do innego stanowiska.
    [Fact]
    public async Task Operator_CannotRead_OtherWorkCenterOrder()
    {
        using var admin = _fixture.CreateClient();

        string adminCsrf = await LoginAsync(
            admin,
            "admin@productionflow.local");

        var ownOrder = await CreateOrderAsync(
            admin,
            adminCsrf);

        var otherOrder = await CreateOrderAsync(
            admin,
            adminCsrf);

        // Przypisujemy operatora do pierwszego stanowiska.
        await SetOperatorWorkCenterAsync(
            ownOrder.WorkCenterId);

        try
        {
            using var operatorClient = _fixture.CreateClient();

            await LoginAsync(
                operatorClient,
                "operator@productionflow.local");

            using var ownResponse = await operatorClient.GetAsync(
                $"/api/orders/{ownOrder.Id}");

            Assert.Equal(
                HttpStatusCode.OK,
                ownResponse.StatusCode);

            using var otherResponse = await operatorClient.GetAsync(
                $"/api/orders/{otherOrder.Id}");

            Assert.Equal(
                HttpStatusCode.Forbidden,
                otherResponse.StatusCode);

            using var historyResponse = await operatorClient.GetAsync(
                $"/api/orders/{otherOrder.Id}/history");

            Assert.Equal(
                HttpStatusCode.Forbidden,
                historyResponse.StatusCode);
        }
        finally
        {
            // Sprzątamy przypisanie, aby inne testy
            // nie zależały od kolejności uruchomienia.
            await SetOperatorWorkCenterAsync(null);
        }
    }

    // TEST 8
    // Planista może zwalniać zlecenia, ale nie może
    // rozpoczynać produkcji.
    [Fact]
    public async Task Start_AsPlanner_Returns403()
    {
        using var admin = _fixture.CreateClient();

        string adminCsrf = await LoginAsync(
            admin,
            "admin@productionflow.local");

        var order = await CreateOrderAsync(
            admin,
            adminCsrf);

        using var releaseResponse = await PostWithCsrfAsync(
            admin,
            $"/api/orders/{order.Id}/release",
            adminCsrf);

        Assert.Equal(
            HttpStatusCode.OK,
            releaseResponse.StatusCode);

        // Tworzymy oddzielne konto planisty na potrzeby testu.
        string plannerEmail = await CreatePlannerAsync();

        using var planner = _fixture.CreateClient();

        string plannerCsrf = await LoginAsync(
            planner,
            plannerEmail);

        using var startResponse = await PostWithCsrfAsync(
            planner,
            $"/api/orders/{order.Id}/start",
            plannerCsrf);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            startResponse.StatusCode);

        var current = await admin
            .GetFromJsonAsync<ProductionOrderResponse>(
                $"/api/orders/{order.Id}");

        Assert.NotNull(current);
        Assert.Equal("Released", current.Status);
    }

    // ===================================================
    // METODY POMOCNICZE
    // ===================================================

    private static string NewOrderNumber()
    {
        return "T21-" + Guid.NewGuid().ToString("N");
    }

    // Logowanie i pobranie nowego tokenu CSRF.
    private static async Task<string> LoginAsync(
        HttpClient client,
        string email)
    {
        string csrf = await GetCsrfTokenAsync(client);

        var login = new LoginRequest
        {
            Email = email,
            Password = ProductionFlowApiFixture.TestPassword
        };

        using var response = await PostWithCsrfAsync(
            client,
            "/api/auth/login",
            csrf,
            login);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        // Po zalogowaniu token musi być pobrany ponownie.
        return await GetCsrfTokenAsync(client);
    }

    private static async Task<string> GetCsrfTokenAsync(
        HttpClient client)
    {
        var result = await client
            .GetFromJsonAsync<CsrfTokenResponse>(
                "/api/auth/csrf-token");

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(
            result.RequestToken));

        return result.RequestToken;
    }

    // Wysyłanie POST z aktualnym tokenem CSRF.
    private static async Task<HttpResponseMessage> PostWithCsrfAsync(
        HttpClient client,
        string url,
        string csrf,
        object? body = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            url);

        request.Headers.Add("X-CSRF-TOKEN", csrf);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    // Tworzenie produktu i stanowiska w testowej bazie.
    private async Task<(Guid ProductId, Guid WorkCenterId)>
        CreateCatalogAsync()
    {
        await using var scope = _fixture.Services
            .CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ProductionFlowDbContext>();

        var product = new Product(
            "P21-" + Guid.NewGuid().ToString("N"),
            "Produkt testowy D21");

        var workCenter = new WorkCenter(
            "WC21-" + Guid.NewGuid().ToString("N"),
            "Stanowisko testowe D21");

        db.Products.Add(product);
        db.WorkCenters.Add(workCenter);

        await db.SaveChangesAsync();

        return (product.Id, workCenter.Id);
    }

    // Tworzenie poprawnego zlecenia przez REST API.
    private async Task<OrderTestData> CreateOrderAsync(
        HttpClient client,
        string csrf)
    {
        var catalog = await CreateCatalogAsync();

        var request = new CreateProductionOrderRequest
        {
            Number = NewOrderNumber(),
            ProductId = catalog.ProductId,
            WorkCenterId = catalog.WorkCenterId,
            PlannedQuantity = 100,
            DueDate = DateOnly.FromDateTime(
                DateTime.UtcNow.AddDays(7))
        };

        using var response = await PostWithCsrfAsync(
            client,
            "/api/orders",
            csrf,
            request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var order = await response.Content
            .ReadFromJsonAsync<ProductionOrderResponse>();

        Assert.NotNull(order);

        return new OrderTestData(
            order.Id,
            order.Number,
            order.ProductId,
            order.WorkCenterId,
            order.DueDate);
    }

    // Przygotowanie zlecenia do raportowania.
    private static async Task ReleaseAndStartAsync(
        HttpClient client,
        Guid orderId,
        string csrf)
    {
        using var release = await PostWithCsrfAsync(
            client,
            $"/api/orders/{orderId}/release",
            csrf);

        Assert.Equal(
            HttpStatusCode.OK,
            release.StatusCode);

        using var start = await PostWithCsrfAsync(
            client,
            $"/api/orders/{orderId}/start",
            csrf);

        Assert.Equal(
            HttpStatusCode.OK,
            start.StatusCode);
    }

    // Przypisanie lub usunięcie przypisania stanowiska.
    private async Task SetOperatorWorkCenterAsync(
        Guid? workCenterId)
    {
        await using var scope = _fixture.Services
            .CreateAsyncScope();

        var manager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await manager.FindByEmailAsync(
            "operator@productionflow.local");

        Assert.NotNull(user);

        user.AssignedWorkCenterId = workCenterId;

        var result = await manager.UpdateAsync(user);

        Assert.True(
            result.Succeeded,
            string.Join(
                "; ",
                result.Errors.Select(error =>
                    error.Description)));
    }

    // Tworzymy dodatkowego planistę bez zmieniania
    // ProductionFlowApiFixture z Dnia 20.
    private async Task<string> CreatePlannerAsync()
    {
        string email = "planner-" +
            Guid.NewGuid().ToString("N") +
            "@productionflow.local";

        await using var scope = _fixture.Services
            .CreateAsyncScope();

        var manager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Planista Test"
        };

        var createResult = await manager.CreateAsync(
            user,
            ProductionFlowApiFixture.TestPassword);

        Assert.True(createResult.Succeeded);

        var roleResult = await manager.AddToRoleAsync(
            user,
            ProductionFlowRoles.Planner);

        Assert.True(roleResult.Succeeded);

        return email;
    }

    private sealed record CsrfTokenResponse(
        string RequestToken);

    private sealed record OrderTestData(
        Guid Id,
        string Number,
        Guid ProductId,
        Guid WorkCenterId,
        DateOnly DueDate);
}
