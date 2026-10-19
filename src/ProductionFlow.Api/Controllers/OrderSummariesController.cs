using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductionFlow.Api.Data;

namespace ProductionFlow.Api.Controllers;

public sealed record OrderSummaryResponse(
    Guid OrderId,
    string Number,
    string Status,
    int PlannedQuantity,
    int GoodQuantity,
    int ScrapQuantity,
    int TotalQuantity,
    int RemainingQuantity,
    decimal ProgressPercent,
    int ReportsCount,
    DateTimeOffset UpdatedAtUtc);

[ApiController]
[Route("api/order-summaries")]
[Authorize(Roles = "Admin,Planner")]
public sealed class OrderSummariesController : ControllerBase
{
    private readonly ProductionFlowDbContext _db;

    public OrderSummariesController(ProductionFlowDbContext db)
    {
        _db = db;
    }

    // GET /api/order-summaries
    [HttpGet]
    public async Task<ActionResult<List<OrderSummaryResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var summaries = await LoadSummariesAsync(cancellationToken);

        return Ok(
            summaries.Select(ToResponse).ToList());
    }

    // GET /api/order-summaries/export.csv
    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv(
        CancellationToken cancellationToken)
    {
        var summaries = await LoadSummariesAsync(cancellationToken);

        var csv = new StringBuilder();

        // Nagłówki kolumn.
        csv.AppendLine(
            "Numer;Status;Planowane;Dobre;Braki;" +
            "Wykonane;Pozostalo;PostepProcent;" +
            "LiczbaRaportow;AktualizacjaUTC");

        var polishCulture = CultureInfo.GetCultureInfo("pl-PL");

        foreach (var item in summaries)
        {
            var response = ToResponse(item);

            csv.Append(CsvText(response.Number)).Append(';');
            csv.Append(CsvText(response.Status)).Append(';');

            csv.Append(response.PlannedQuantity).Append(';');
            csv.Append(response.GoodQuantity).Append(';');
            csv.Append(response.ScrapQuantity).Append(';');
            csv.Append(response.TotalQuantity).Append(';');
            csv.Append(response.RemainingQuantity).Append(';');

            csv.Append(response.ProgressPercent.ToString(
                "0.00",
                polishCulture)).Append(';');

            csv.Append(response.ReportsCount).Append(';');

            csv.Append(response.UpdatedAtUtc.ToString(
                "O",
                CultureInfo.InvariantCulture));

            csv.AppendLine();
        }

        // UTF-8 BOM ułatwia otwarcie polskich znaków w Excelu.
        byte[] content = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: true)
            .GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv.ToString()))
            .ToArray();

        string fileName =
            $"productionflow-summaries-{DateTime.UtcNow:yyyyMMdd}.csv";

        return File(
            content,
            "text/csv; charset=utf-8",
            fileName);
    }

    private Task<List<OrderProductionSummary>> LoadSummariesAsync(
        CancellationToken cancellationToken)
    {
        return _db.OrderProductionSummaries
            .AsNoTracking()
            .OrderBy(item => item.Number)
            .ThenBy(item => item.OrderId)
            .ToListAsync(cancellationToken);
    }

    private static OrderSummaryResponse ToResponse(
        OrderProductionSummary item)
    {
        int total = item.GoodQuantity + item.ScrapQuantity;

        int remaining = item.PlannedQuantity - total;

        decimal progress = item.PlannedQuantity == 0
            ? 0
            : Math.Round(
                100m * total / item.PlannedQuantity,
                2);

        return new OrderSummaryResponse(
            item.OrderId,
            item.Number,
            item.Status.ToString(),
            item.PlannedQuantity,
            item.GoodQuantity,
            item.ScrapQuantity,
            total,
            remaining,
            progress,
            item.ReportsCount,
            item.UpdatedAtUtc);
    }

    private static string CsvText(string value)
    {
        // Usuwamy znaki końca linii z komórki.
        string safe = value
            .Replace("\r", " ")
            .Replace("\n", " ");

        // Nie pozwalamy interpretować numeru zlecenia
        // jako formuły w aplikacji arkuszowej.
        string trimmed = safe.TrimStart();

        if (trimmed.Length > 0 &&
            "=+-@\t".Contains(trimmed[0]))
        {
            safe = "'" + safe;
        }

        // Cytowanie pola zgodnie z regułami CSV.
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}