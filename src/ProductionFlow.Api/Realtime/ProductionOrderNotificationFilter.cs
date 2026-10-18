
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProductionFlow.Api.Controllers;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Dtos;

namespace ProductionFlow.Api.Realtime;

public sealed class ProductionOrderNotificationFilter
    : IAsyncActionFilter
{
    private static readonly IReadOnlyDictionary<string, string>
        ChangeTypes = new Dictionary<string, string>
        {
            [nameof(OrdersController.Create)] = "Created",
            [nameof(OrdersController.Release)] = "Released",
            [nameof(OrdersController.Start)] = "Started",
            [nameof(OrdersController.Cancel)] = "Cancelled",
            [nameof(OrdersController.AddReport)] = "ReportAdded",
            [nameof(OrdersController.Complete)] = "Completed"
        };

    private readonly ProductionFlowDbContext _db;
    private readonly IHubContext<ProductionHub> _hub;
    private readonly ILogger<ProductionOrderNotificationFilter> _logger;

    public ProductionOrderNotificationFilter(
        ProductionFlowDbContext db,
        IHubContext<ProductionHub> hub,
        ILogger<ProductionOrderNotificationFilter> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        // Najpierw wykonujemy normalną akcję kontrolera.
        ActionExecutedContext executed = await next();

        // Nie powiadamiamy o anulowanej lub błędnej akcji.
        if (executed.Canceled ||
            (executed.Exception is not null &&
             !executed.ExceptionHandled))
        {
            return;
        }

        // Interesują nas wyłącznie akcje OrdersController.
        if (context.ActionDescriptor
            is not ControllerActionDescriptor descriptor ||
            descriptor.ControllerTypeInfo.AsType()
                != typeof(OrdersController))
        {
            return;
        }

        // Pomijamy akcje GET i inne operacje.
        if (!ChangeTypes.TryGetValue(
            descriptor.ActionName,
            out string? changeType))
        {
            return;
        }

        // Sprawdzamy wynik zwrócony przez kontroler.
        if (executed.Result is not ObjectResult result)
        {
            return;
        }

        int statusCode = result.StatusCode ??
            StatusCodes.Status200OK;

        // Wysyłamy zdarzenia wyłącznie po sukcesie HTTP.
        if (statusCode < 200 || statusCode >= 300)
        {
            return;
        }

        Guid orderId;

        // Przy tworzeniu ID znajduje się w odpowiedzi.
        if (descriptor.ActionName ==
            nameof(OrdersController.Create))
        {
            if (result.Value
                is not ProductionOrderResponse createdOrder)
            {
                return;
            }

            orderId = createdOrder.Id;
        }
        else
        {
            // Przy pozostałych operacjach ID pochodzi z trasy.
            if (!context.ActionArguments.TryGetValue(
                    "id", out object? argument) ||
                argument is not Guid id)
            {
                return;
            }

            orderId = id;
        }

        try
        {
            // Kontroler zakończył już zapis i transakcję.
            // Pobieramy aktualny status ze źródła danych.
            var order = await _db.ProductionOrders
                .AsNoTracking()
                .Where(item => item.Id == orderId)
                .Select(item => new
                {
                    item.WorkCenterId,
                    item.Status
                })
                .SingleOrDefaultAsync();

            if (order is null)
            {
                return;
            }

            var message = new ProductionOrderChanged(
                orderId,
                order.WorkCenterId,
                changeType,
                order.Status.ToString(),
                DateTimeOffset.UtcNow);

            // Planista i administrator otrzymują wszystkie zmiany.
            Task planningNotification = _hub.Clients
                .Group(ProductionHubGroups.Planning)
                .SendAsync("OrderChanged", message);

            // Operatorzy otrzymują zmiany przypisanego stanowiska.
            Task operatorNotification = _hub.Clients
                .Group(ProductionHubGroups.ForWorkCenter(
                    order.WorkCenterId))
                .SendAsync("OrderChanged", message);

            await Task.WhenAll(
                planningNotification,
                operatorNotification);

            _logger.LogInformation(
                "SignalR OrderChanged: " +
                "OrderId={OrderId}, ChangeType={ChangeType}, " +
                "Status={Status}",
                orderId,
                changeType,
                message.Status);
        }
        catch (Exception exception)
        {
            // Błąd powiadomienia nie może unieważniać
            // zakończonej operacji w PostgreSQL.
            _logger.LogWarning(
                exception,
                "Nie udało się wysłać zdarzenia SignalR " +
                "dla zlecenia {OrderId}",
                orderId);
        }
    }
}
