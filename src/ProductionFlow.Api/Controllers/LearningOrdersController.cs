using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using ProductionFlow.Api.Learning;

namespace ProductionFlow.Api.Controllers;

[ApiController]
[Route("api/learning/orders")]
public sealed class LearningOrdersController : ControllerBase
{
    private readonly LearningOrderService _service;
    private readonly ILogger<LearningOrdersController> _logger;

    public LearningOrdersController(
        LearningOrderService service,
        ILogger<LearningOrdersController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LearningOrderDto>> GetByIdAsync(
        int id,
        CancellationToken cancellationToken,
        [FromQuery] bool simulateFailure = false)
    {
        if (id <= 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Nieprawidłowy identyfikator.",
                detail: "Identyfikator musi być większy od zera.");
        }

        try
        {
            LearningOrderDto? order = await _service.GetByIdAsync(
                id,
                simulateFailure,
                cancellationToken);

            if (order is null)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Nie znaleziono zlecenia.",
                    detail: "Brak przykładowego zlecenia o podanym identyfikatorze.");
            }

            return Ok(order);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Źródło danych jest niedostępne dla zlecenia {OrderId}.",
                id);

            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Źródło danych jest niedostępne.",
                detail: "Spróbuj ponownie później.");
        }
    }
}