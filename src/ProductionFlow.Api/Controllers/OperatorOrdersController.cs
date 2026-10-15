using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Dtos;
using ProductionFlow.Api.Identity;

namespace ProductionFlow.Api.Controllers;

[ApiController]
[Route("api/operator/orders")]
public sealed class OperatorOrdersController : ControllerBase
{
    private readonly ProductionFlowDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public OperatorOrdersController(
        ProductionFlowDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // GET /api/operator/orders
    [HttpGet]
    public async Task<ActionResult<List<ProductionOrderResponse>>>
        GetMine(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        if (!user.AssignedWorkCenterId.HasValue)
        {
            return Ok(new List<ProductionOrderResponse>());
        }

        var workCenterId = user.AssignedWorkCenterId.Value;

        var orders = await _db.ProductionOrders
            .AsNoTracking()
            .Where(item => item.WorkCenterId == workCenterId)
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.Number)
            .Take(100)
            .Select(item => new ProductionOrderResponse(
                item.Id,
                item.Number,
                item.ProductId,
                item.WorkCenterId,
                item.PlannedQuantity,
                item.DueDate,
                item.Status.ToString()))
            .ToListAsync(cancellationToken);

        return Ok(orders);
    }
}