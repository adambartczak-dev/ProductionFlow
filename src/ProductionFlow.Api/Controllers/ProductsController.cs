using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Domain;
using ProductionFlow.Api.Dtos;

namespace ProductionFlow.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly ProductionFlowDbContext _db;

    public ProductsController(ProductionFlowDbContext db)
    {
        _db = db;
    }

    // GET /api/products
    [HttpGet]
    public async Task<ActionResult<List<ProductResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var products = await _db.Products
            .AsNoTracking()
            .OrderBy(item => item.Code)
            .Select(item => new ProductResponse(
                item.Id,
                item.Code,
                item.Name))
            .ToListAsync(cancellationToken);

        return Ok(products);
    }

    // GET /api/products/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new ProductResponse(
                item.Id,
                item.Code,
                item.Name))
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    // POST /api/products
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(
        [FromBody] ProductRequest request,
        CancellationToken cancellationToken)
    {
        string code = request.Code!.Trim();
        string name = request.Name!.Trim();

        bool codeExists = await _db.Products
            .AnyAsync(
                item => item.Code == code,
                cancellationToken);

        if (codeExists)
        {
            return CodeConflict(code);
        }

        var product = new Product(code, name);

        _db.Products.Add(product);

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

        var response = new ProductResponse(
            product.Id,
            product.Code,
            product.Name);

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            response);
    }

    // PUT /api/products/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] ProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        string code = request.Code!.Trim();
        string name = request.Name!.Trim();

        bool codeExists = await _db.Products
            .AnyAsync(
                item => item.Code == code && item.Id != id,
                cancellationToken);

        if (codeExists)
        {
            return CodeConflict(code);
        }

        product.EditDetails(code, name);

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
            Title = "Kod produktu już istnieje.",
            Detail = $"Produkt o kodzie {code} już istnieje."
        });
    }
}
