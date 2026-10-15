
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Domain;
using ProductionFlow.Api.Dtos;

namespace ProductionFlow.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly ProductionFlowDbContext _db;

    public OrdersController(ProductionFlowDbContext db)
    {
        _db = db;
    }

    // GET /api/orders
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductionOrderResponse>>> GetAll(
        [FromQuery] ProductionOrderListQuery query,
        CancellationToken cancellationToken)
    {
        string sortBy = query.SortBy?
            .Trim()
            .ToLowerInvariant() ?? "";

        string sortDirection = query.SortDirection?
            .Trim()
            .ToLowerInvariant() ?? "";

        if (sortBy is not
            ("number" or "duedate" or "plannedquantity" or "status"))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Nieprawidłowe pole sortowania.",
                Detail =
                    "Dozwolone: number, dueDate, plannedQuantity, status."
            });
        }

        if (sortDirection is not ("asc" or "desc"))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Nieprawidłowy kierunek sortowania.",
                Detail = "Dozwolone: asc, desc."
            });
        }

        IQueryable<ProductionOrder> ordersQuery =
            _db.ProductionOrders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            bool parsed = Enum.TryParse<ProductionOrderStatus>(
                query.Status.Trim(),
                ignoreCase: true,
                out var status);

            if (!parsed || !Enum.IsDefined(status))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Nieprawidłowy status zlecenia.",
                    Detail =
                        "Dozwolone: Draft, Released, InProgress, " +
                        "Completed, Cancelled."
                });
            }

            ordersQuery = ordersQuery.Where(
                item => item.Status == status);
        }

        if (query.ProductId.HasValue)
        {
            Guid productId = query.ProductId.Value;

            ordersQuery = ordersQuery.Where(
                item => item.ProductId == productId);
        }

        if (query.WorkCenterId.HasValue)
        {
            Guid workCenterId = query.WorkCenterId.Value;

            ordersQuery = ordersQuery.Where(
                item => item.WorkCenterId == workCenterId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string pattern = $"%{query.Search.Trim()}%";

            ordersQuery = ordersQuery.Where(
                item => EF.Functions.ILike(
                    item.Number,
                    pattern));
        }

        int totalCount = await ordersQuery.CountAsync(
            cancellationToken);

        bool descending = sortDirection == "desc";

        IOrderedQueryable<ProductionOrder> sortedOrders =
            sortBy switch
            {
                "number" when descending =>
                    ordersQuery.OrderByDescending(
                        item => item.Number),

                "number" =>
                    ordersQuery.OrderBy(
                        item => item.Number),

                "duedate" when descending =>
                    ordersQuery.OrderByDescending(
                        item => item.DueDate),

                "duedate" =>
                    ordersQuery.OrderBy(
                        item => item.DueDate),

                "plannedquantity" when descending =>
                    ordersQuery.OrderByDescending(
                        item => item.PlannedQuantity),

                "plannedquantity" =>
                    ordersQuery.OrderBy(
                        item => item.PlannedQuantity),

                "status" when descending =>
                    ordersQuery.OrderByDescending(
                        item => item.Status),

                "status" =>
                    ordersQuery.OrderBy(
                        item => item.Status),

                _ => ordersQuery.OrderBy(
                    item => item.DueDate)
            };

        sortedOrders = sortedOrders.ThenBy(
            item => item.Id);

        int totalPages = totalCount / query.PageSize;

        if (totalCount % query.PageSize != 0)
        {
            totalPages++;
        }

        long offset = ((long)query.Page - 1) * query.PageSize;

        List<ProductionOrder> orders;

        if (offset >= totalCount)
        {
            orders = new List<ProductionOrder>();
        }
        else
        {
            orders = await sortedOrders
                .Skip((int)offset)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
        }

        List<ProductionOrderResponse> items = orders
            .Select(ToResponse)
            .ToList();

        var result = new PagedResult<ProductionOrderResponse>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            totalPages);

        return Ok(result);
    }

    // GET /api/orders/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductionOrderResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await _db.ProductionOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (order is null)
        {
            return OrderNotFound();
        }

        return Ok(ToResponse(order));
    }

    // POST /api/orders
    [HttpPost]
    public async Task<ActionResult<ProductionOrderResponse>> Create(
        [FromBody] CreateProductionOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ProductId == Guid.Empty)
        {
            ModelState.AddModelError(
                nameof(request.ProductId),
                "Id produktu jest wymagane.");
        }

        if (request.WorkCenterId == Guid.Empty)
        {
            ModelState.AddModelError(
                nameof(request.WorkCenterId),
                "Id stanowiska jest wymagane.");
        }

        if (request.DueDate == default)
        {
            ModelState.AddModelError(
                nameof(request.DueDate),
                "Termin zlecenia jest wymagany.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        string number = request.Number!.Trim();

        bool productExists = await _db.Products
            .AnyAsync(
                item => item.Id == request.ProductId,
                cancellationToken);

        if (!productExists)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Nie znaleziono produktu.",
                Detail = "Produkt o podanym Id nie istnieje."
            });
        }

        bool workCenterExists = await _db.WorkCenters
            .AnyAsync(
                item => item.Id == request.WorkCenterId,
                cancellationToken);

        if (!workCenterExists)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Nie znaleziono stanowiska.",
                Detail = "Stanowisko o podanym Id nie istnieje."
            });
        }

        bool numberExists = await _db.ProductionOrders
            .AnyAsync(
                item => item.Number == number,
                cancellationToken);

        if (numberExists)
        {
            return NumberConflict(number);
        }

        var order = new ProductionOrder(
            number,
            request.ProductId,
            request.WorkCenterId,
            request.PlannedQuantity,
            request.DueDate);

        _db.ProductionOrders.Add(order);

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
            return NumberConflict(number);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.ForeignKeyViolation
            })
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Nie można zapisać zlecenia.",
                Detail =
                    "Produkt lub stanowisko nie jest już dostępne."
            });
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = order.Id },
            ToResponse(order));
    }

    // POST /api/orders/{id}/release
    [HttpPost("{id:guid}/release")]
    public async Task<ActionResult<ProductionOrderResponse>> Release(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await ChangeStatusAsync(
            id,
            order => order.Release(),
            cancellationToken);
    }

    // POST /api/orders/{id}/start
    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<ProductionOrderResponse>> Start(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await ChangeStatusAsync(
            id,
            order => order.Start(),
            cancellationToken);
    }

    // POST /api/orders/{id}/cancel
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ProductionOrderResponse>> Cancel(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await ChangeStatusAsync(
            id,
            order => order.Cancel(),
            cancellationToken);
    }

    // GET /api/orders/{id}/reports
    [HttpGet("{id:guid}/reports")]
    public async Task<ActionResult<List<ExecutionReportResponse>>> GetReports(
        Guid id,
        CancellationToken cancellationToken)
    {
        bool orderExists = await _db.ProductionOrders
            .AsNoTracking()
            .AnyAsync(
                item => item.Id == id,
                cancellationToken);

        if (!orderExists)
        {
            return OrderNotFound();
        }

        var reports = await _db.ExecutionReports
            .AsNoTracking()
            .Where(item => item.ProductionOrderId == id)
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return Ok(reports.Select(ToReportResponse).ToList());
    }

    // GET /api/orders/{id}/reports/{reportId}
    [HttpGet("{id:guid}/reports/{reportId:guid}")]
    public async Task<ActionResult<ExecutionReportResponse>> GetReportById(
        Guid id,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var report = await _db.ExecutionReports
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == reportId
                    && item.ProductionOrderId == id,
                cancellationToken);

        if (report is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Nie znaleziono raportu.",
                Detail =
                    "Raport o podanym Id nie istnieje w tym zleceniu."
            });
        }

        return Ok(ToReportResponse(report));
    }

    // POST /api/orders/{id}/reports
    [HttpPost("{id:guid}/reports")]
    public async Task<ActionResult<ExecutionReportResponse>> AddReport(
        Guid id,
        [FromBody] CreateExecutionReportRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OperatorId))
        {
            ModelState.AddModelError(
                nameof(request.OperatorId),
                "Operator jest wymagany.");
        }

        if (request.GoodQuantity == 0
            && request.ScrapQuantity == 0)
        {
            ModelState.AddModelError(
                nameof(request.GoodQuantity),
                "Raport musi zawierać co najmniej jedną sztukę.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // Rozpoczynamy transakcję.
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        // Blokujemy zlecenie, a następnie pobieramy raporty.
        var order = await GetLockedOrderAsync(
            id,
            loadReports: true,
            cancellationToken);

        if (order is null)
        {
            return OrderNotFound();
        }

        ExecutionReport report;

        try
        {
            report = order.AddReport(
                request.OperatorId!,
                request.GoodQuantity,
                request.ScrapQuantity);
        }
        catch (InvalidOperationException exception)
        {
            return BusinessConflict(exception);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Nieprawidłowe dane raportu.",
                Detail = exception.Message
            });
        }

        // Zapisujemy raport.
        await _db.SaveChangesAsync(cancellationToken);

        // Zatwierdzamy transakcję i zwalniamy blokadę.
        await transaction.CommitAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetReportById),
            new { id, reportId = report.Id },
            ToReportResponse(report));
    }

    // GET /api/orders/{id}/progress
    [HttpGet("{id:guid}/progress")]
    public async Task<ActionResult<ProductionProgressResponse>> GetProgress(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await _db.ProductionOrders
            .AsNoTracking()
            .Include(item => item.ExecutionReports)
            .FirstOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (order is null)
        {
            return OrderNotFound();
        }

        return Ok(ToProgressResponse(order));
    }

    // POST /api/orders/{id}/complete
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<ProductionProgressResponse>> Complete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        // Blokujemy zlecenie i ładujemy wszystkie raporty.
        var order = await GetLockedOrderAsync(
            id,
            loadReports: true,
            cancellationToken);

        if (order is null)
        {
            return OrderNotFound();
        }

        try
        {
            order.Complete();
        }
        catch (InvalidOperationException exception)
        {
            return BusinessConflict(exception);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Ok(ToProgressResponse(order));
    }

    // Wspólna obsługa zmian statusu.
    private async Task<ActionResult<ProductionOrderResponse>>
        ChangeStatusAsync(
            Guid id,
            Action<ProductionOrder> changeStatus,
            CancellationToken cancellationToken)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        // Statusy nie wymagają ładowania raportów.
        var order = await GetLockedOrderAsync(
            id,
            loadReports: false,
            cancellationToken);

        if (order is null)
        {
            return OrderNotFound();
        }

        try
        {
            changeStatus(order);
        }
        catch (InvalidOperationException exception)
        {
            return BusinessConflict(exception);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Ok(ToResponse(order));
    }

    // Pobieranie zlecenia z blokadą PostgreSQL.
    // Wywołujemy wyłącznie wewnątrz aktywnej transakcji.
    private async Task<ProductionOrder?> GetLockedOrderAsync(
        Guid id,
        bool loadReports,
        CancellationToken cancellationToken)
    {
        var order = await _db.ProductionOrders
            .FromSqlInterpolated(
                $@"SELECT * FROM ""ProductionOrders""
                   WHERE ""Id"" = {id}
                   FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (order is not null && loadReports)
        {
            // Raporty pobieramy dopiero po uzyskaniu blokady.
            await _db.Entry(order)
                .Collection(item => item.ExecutionReports)
                .LoadAsync(cancellationToken);
        }

        return order;
    }

    private static ProductionOrderResponse ToResponse(
        ProductionOrder order)
    {
        return new ProductionOrderResponse(
            order.Id,
            order.Number,
            order.ProductId,
            order.WorkCenterId,
            order.PlannedQuantity,
            order.DueDate,
            order.Status.ToString());
    }

    private static ExecutionReportResponse ToReportResponse(
        ExecutionReport report)
    {
        return new ExecutionReportResponse(
            report.Id,
            report.ProductionOrderId,
            report.OperatorId,
            report.GoodQuantity,
            report.ScrapQuantity,
            report.TotalQuantity,
            report.CreatedAtUtc);
    }

    private static ProductionProgressResponse ToProgressResponse(
        ProductionOrder order)
    {
        return new ProductionProgressResponse(
            order.Id,
            order.Number,
            order.Status.ToString(),
            order.PlannedQuantity,
            order.GoodQuantity,
            order.ScrapQuantity,
            order.TotalQuantity,
            order.RemainingQuantity,
            order.ProgressPercent,
            order.ExecutionReports.Count);
    }

    private NotFoundObjectResult OrderNotFound()
    {
        return NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Nie znaleziono zlecenia.",
            Detail = "Zlecenie o podanym Id nie istnieje."
        });
    }

    private ConflictObjectResult BusinessConflict(
        InvalidOperationException exception)
    {
        return Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Niedozwolona operacja na zleceniu.",
            Detail = exception.Message
        });
    }

    private ConflictObjectResult NumberConflict(string number)
    {
        return Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Numer zlecenia już istnieje.",
            Detail = $"Zlecenie o numerze {number} już istnieje."
        });
    }
}
