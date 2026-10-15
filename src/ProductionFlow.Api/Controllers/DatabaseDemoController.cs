
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Domain;

namespace ProductionFlow.Api.Controllers;

[ApiController]
[Route("api/learning/database-demo")]
public sealed class DatabaseDemoController : ControllerBase
{
    private readonly ProductionFlowDbContext _db;

    public DatabaseDemoController(ProductionFlowDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> CreateDemoData(
        CancellationToken cancellationToken)
    {
        // Produkt demonstracyjny z Dnia 6
        var previousProduct = await _db.Products
            .FirstOrDefaultAsync(
                item => item.Code == "P-DB-001",
                cancellationToken);

        if (previousProduct is null)
        {
            previousProduct = new Product(
                "P-DB-001",
                "Produkt testowy bazy");

            _db.Products.Add(previousProduct);
        }

        // Stanowisko demonstracyjne z Dnia 6
        var previousWorkCenter = await _db.WorkCenters
            .FirstOrDefaultAsync(
                item => item.Code == "WC-DB-01",
                cancellationToken);

        if (previousWorkCenter is null)
        {
            previousWorkCenter = new WorkCenter(
                "WC-DB-01",
                "Stanowisko testowe bazy");

            _db.WorkCenters.Add(previousWorkCenter);
        }

        // Produkt dla przykładowego zlecenia
        var product = await _db.Products
            .FirstOrDefaultAsync(
                item => item.Code == "P-001",
                cancellationToken);

        if (product is null)
        {
            product = new Product("P-001", "Obudowa");
            _db.Products.Add(product);
        }

        // Stanowisko dla przykładowego zlecenia
        var workCenter = await _db.WorkCenters
            .FirstOrDefaultAsync(
                item => item.Code == "WC-01",
                cancellationToken);

        if (workCenter is null)
        {
            workCenter = new WorkCenter("WC-01", "Montaż");
            _db.WorkCenters.Add(workCenter);
        }

        // Sprawdzenie, czy zlecenie już istnieje
        var order = await _db.ProductionOrders
            .Include(item => item.ExecutionReports)
            .FirstOrDefaultAsync(
                item => item.Number == "PF-DB-0001",
                cancellationToken);

        if (order is null)
        {
            order = new ProductionOrder(
                "PF-DB-0001",
                product.Id,
                workCenter.Id,
                100,
                DateOnly.FromDateTime(
                    DateTime.UtcNow.AddDays(7)));

            order.Release();
            order.Start();

            // Pierwszy raport: 60 sztuk
            order.AddReport("demo-operator", 57, 3);

            // Drugi raport: 40 sztuk
            order.AddReport("demo-operator", 38, 2);

            // Razem: 100 sztuk
            order.Complete();

            _db.ProductionOrders.Add(order);
        }

        // Zapis wszystkich nowych rekordów
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            Message = "Dane demonstracyjne są zapisane w bazie.",
            ProductId = previousProduct.Id,
            WorkCenterId = previousWorkCenter.Id,
            ProductionOrderId = order.Id,
            ReportsCount = order.ExecutionReports.Count
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetDemoData(
        CancellationToken cancellationToken)
    {
        var products = await _db.Products
            .AsNoTracking()
            .OrderBy(item => item.Code)
            .Select(item => new
            {
                item.Id,
                item.Code,
                item.Name
            })
            .ToListAsync(cancellationToken);

        var workCenters = await _db.WorkCenters
            .AsNoTracking()
            .OrderBy(item => item.Code)
            .Select(item => new
            {
                item.Id,
                item.Code,
                item.Name
            })
            .ToListAsync(cancellationToken);

        var orders = await _db.ProductionOrders
            .AsNoTracking()
            .Include(item => item.ExecutionReports)
            .OrderBy(item => item.Number)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            Products = products,
            WorkCenters = workCenters,

            Orders = orders.Select(item => new
            {
                item.Id,
                item.Number,
                item.ProductId,
                item.WorkCenterId,
                Status = item.Status.ToString(),
                item.PlannedQuantity,
                item.DueDate,
                item.GoodQuantity,
                item.ScrapQuantity,
                item.TotalQuantity,
                item.ProgressPercent,

                ReportsCount = item.ExecutionReports.Count,

                Reports = item.ExecutionReports
                    .OrderBy(report => report.CreatedAtUtc)
                    .Select(report => new
                    {
                        report.Id,
                        report.OperatorId,
                        report.GoodQuantity,
                        report.ScrapQuantity,
                        report.CreatedAtUtc
                    })
                    .ToList()
            }).ToList()
        });
    }
}
