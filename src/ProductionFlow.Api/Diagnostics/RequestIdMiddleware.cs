
using System.Diagnostics;
using Serilog.Context;

namespace ProductionFlow.Api.Diagnostics;

public sealed class RequestIdMiddleware
{
    private readonly RequestDelegate _next;

    public RequestIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Wykorzystujemy identyfikator bieżącego śledzenia
        // ASP.NET Core, a jeśli go nie ma — TraceIdentifier.
        string requestId =
            Activity.Current?.Id ?? context.TraceIdentifier;

        // Ten sam identyfikator będzie widoczny
        // przez HttpContext.TraceIdentifier.
        context.TraceIdentifier = requestId;

        // Dodajemy identyfikator do odpowiedzi HTTP.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Request-ID"] =
                requestId;

            return Task.CompletedTask;
        });

        // Wszystkie logi w ramach bieżącego żądania
        // mogą korzystać z właściwości RequestId.
        using (LogContext.PushProperty(
            "RequestId",
            requestId))
        {
            await _next(context);
        }
    }
}
