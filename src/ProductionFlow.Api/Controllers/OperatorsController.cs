using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Dtos;
using ProductionFlow.Api.Identity;

namespace ProductionFlow.Api.Controllers;

[ApiController]
[Route("api/operators")]
public sealed class OperatorsController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ProductionFlowDbContext _db;

    public OperatorsController(
        UserManager<ApplicationUser> userManager,
        ProductionFlowDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    // GET /api/operators
    [HttpGet]
    public async Task<ActionResult<List<OperatorAssignmentResponse>>>
        GetAll()
    {
        var users = await _userManager.GetUsersInRoleAsync(
            ProductionFlowRoles.Operator);

        var result = users
            .OrderBy(user => user.Email)
            .Select(user => new OperatorAssignmentResponse(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                user.AssignedWorkCenterId))
            .ToList();

        return Ok(result);
    }

    // PUT /api/operators/{id}/workcenter
    [HttpPut("{id:guid}/workcenter")]
    public async Task<IActionResult> AssignWorkCenter(
        Guid id,
        [FromBody] AssignWorkCenterRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WorkCenterId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Nieprawidłowe stanowisko.",
                Detail = "Id stanowiska jest wymagane."
            });
        }

        var user = await _userManager.FindByIdAsync(
            id.ToString());

        if (user is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Nie znaleziono użytkownika."
            });
        }

        bool isOperator = await _userManager.IsInRoleAsync(
            user,
            ProductionFlowRoles.Operator);

        if (!isOperator)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Użytkownik nie jest operatorem."
            });
        }

        bool workCenterExists = await _db.WorkCenters
            .AsNoTracking()
            .AnyAsync(
                item => item.Id == request.WorkCenterId,
                cancellationToken);

        if (!workCenterExists)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Nie znaleziono stanowiska."
            });
        }

        user.AssignedWorkCenterId = request.WorkCenterId;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Nie udało się zapisać przypisania.");
        }

        return NoContent();
    }
}