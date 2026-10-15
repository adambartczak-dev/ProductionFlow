using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Domain;
using ProductionFlow.Api.Dtos;

namespace ProductionFlow.Api.Controllers;

[ApiController]
[Route("api/workcenters")]
public sealed class WorkCentersController : ControllerBase
{
    private readonly ProductionFlowDbContext _db;

    public WorkCentersController(ProductionFlowDbContext db)
    {
        _db = db;
    }

    // GET /api/workcenters
    [HttpGet]
    public async Task<ActionResult<List<WorkCenterResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var workCenters = await _db.WorkCenters
            .AsNoTracking()
            .OrderBy(item => item.Code)
            .Select(item => new WorkCenterResponse(
                item.Id,
                item.Code,
                item.Name))
            .ToListAsync(cancellationToken);

        return Ok(workCenters);
    }

    // GET /api/workcenters/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkCenterResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var workCenter = await _db.WorkCenters
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new WorkCenterResponse(
                item.Id,
                item.Code,
                item.Name))
            .FirstOrDefaultAsync(cancellationToken);

        if (workCenter is null)
        {
            return NotFound();
        }

        return Ok(workCenter);
    }

    // POST /api/workcenters
    [HttpPost]
    public async Task<ActionResult<WorkCenterResponse>> Create(
        [FromBody] WorkCenterRequest request,
        CancellationToken cancellationToken)
    {
        string code = request.Code!.Trim();
        string name = request.Name!.Trim();

        bool codeExists = await _db.WorkCenters
            .AnyAsync(
                item => item.Code == code,
                cancellationToken);

        if (codeExists)
        {
            return CodeConflict(code);
        }

        var workCenter = new WorkCenter(code, name);

        _db.WorkCenters.Add(workCenter);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            return CodeConflict(code);
        }

        var response = new WorkCenterResponse(
            workCenter.Id,
            workCenter.Code,
            workCenter.Name);

        return CreatedAtAction(
            nameof(GetById),
            new { id = workCenter.Id },
            response);
    }

    // PUT /api/workcenters/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] WorkCenterRequest request,
        CancellationToken cancellationToken)
    {
        var workCenter = await _db.WorkCenters
            .FirstOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (workCenter is null)
        {
            return NotFound();
        }

        string code = request.Code!.Trim();
        string name = request.Name!.Trim();

        bool codeExists = await _db.WorkCenters
            .AnyAsync(
                item => item.Code == code && item.Id != id,
                cancellationToken);

        if (codeExists)
        {
            return CodeConflict(code);
        }

        workCenter.EditDetails(code, name);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            return CodeConflict(code);
        }

        return NoContent();
    }

    private ConflictObjectResult CodeConflict(string code)
    {
        return Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Kod stanowiska już istnieje.",
            Detail = $"Stanowisko o kodzie {code} już istnieje."
        });
    }
}
