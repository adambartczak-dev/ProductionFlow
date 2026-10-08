using Microsoft.AspNetCore.Mvc;
using ProductionFlow.Api.Domain;

namespace ProductionFlow.Api.Controllers;

[ApiController]
[Route("api/learning/domain-demo")]
public sealed class DomainDemoController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var product = new Product("P-001", "Obudowa");
        var workCenter = new WorkCenter("WC-01", "Montaż");

        var order = new ProductionOrder(
            "PF-0001",
            product.Id,
            workCenter.Id,
            100,
            DateOnly.FromDateTime(DateTime.Today.AddDays(7)));

        order.Release();
        order.Start();
        order.AddReport("demo-operator", 57, 3);

        bool overPlanRejected = false;
        bool earlyCompletionRejected = false;

        try
        {
            order.AddReport("demo-operator", 41, 0);
        }
        catch (InvalidOperationException)
        {
            overPlanRejected = true;
        }

        int quantityAfterRejectedReport = order.TotalQuantity;

        try
        {
            order.Complete();
        }
        catch (InvalidOperationException)
        {
            earlyCompletionRejected = true;
        }

        order.AddReport("demo-operator", 38, 2);
        string statusBeforeCompletion = order.Status.ToString();
        order.Complete();

        return Ok(new
        {
            order.Number,
            Product = product.Name,
            WorkCenter = workCenter.Name,
            Status = order.Status.ToString(),
            StatusBeforeCompletion = statusBeforeCompletion,
            order.PlannedQuantity,
            order.GoodQuantity,
            order.ScrapQuantity,
            order.TotalQuantity,
            order.RemainingQuantity,
            order.ProgressPercent,
            ReportsCount = order.ExecutionReports.Count,
            OverPlanRejected = overPlanRejected,
            QuantityAfterRejectedReport = quantityAfterRejectedReport,
            EarlyCompletionRejected = earlyCompletionRejected
        });
    }
}