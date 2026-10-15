
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Dtos;

namespace ProductionFlow.Api.Controllers;

[ApiController]
[Route("api/orders/{orderId:guid}/history")]
public sealed class OrderHistoryController : ControllerBase
{
    private readonly ProductionFlowDbContext _db;

    public OrderHistoryController(ProductionFlowDbContext db)
    {
        _db = db;
    }

    // GET /api/orders/{orderId}/history
    [HttpGet]
    public async Task<ActionResult<List<OrderHistoryResponse>>> GetHistory(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        bool orderExists = await _db.ProductionOrders
            .AsNoTracking()
            .AnyAsync(
                item => item.Id == orderId,
                cancellationToken);

        if (!orderExists)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Nie znaleziono zlecenia.",
                Detail = "Zlecenie o podanym Id nie istnieje."
            });
        }

        var history = await _db.OrderHistoryEntries
            .AsNoTracking()
            .Where(item => item.ProductionOrderId == orderId)
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .Select(item => new OrderHistoryResponse(
                item.Id,
                item.ProductionOrderId,
                item.EventType,
                item.Details,
                item.Actor,
                item.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(history);
    }
}
