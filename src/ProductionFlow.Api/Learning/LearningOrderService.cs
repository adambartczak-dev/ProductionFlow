using System.Net.Http;

namespace ProductionFlow.Api.Learning;

public sealed class LearningOrderService
{
    private readonly ILogger<LearningOrderService> _logger;

    public LearningOrderService(ILogger<LearningOrderService> logger)
    {
        _logger = logger;
    }

    public async Task<LearningOrderDto?> GetByIdAsync(
        int id,
        bool simulateFailure,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Rozpoczęto odczyt przykładowego zlecenia {OrderId}.",
            id);

        try
        {
            // Symulacja oczekiwania na źródło danych.
            await Task.Delay(
                TimeSpan.FromSeconds(3),
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (simulateFailure)
            {
                // Kontrolowana symulacja awarii.
                // Nie wykonujemy tutaj prawdziwego żądania HTTP.
                throw new HttpRequestException(
                    "Symulowana niedostępność źródła danych.");
            }

            LearningOrderDto? order = id switch
            {
                1 => new LearningOrderDto(
                    1,
                    "DEMO-001",
                    "Przykładowe zlecenie z uwagami."),

                2 => new LearningOrderDto(
                    2,
                    "DEMO-002",
                    null),

                _ => null
            };

            _logger.LogInformation(
                "Zakończono odczyt przykładowego zlecenia {OrderId}.",
                id);

            return order;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Anulowano odczyt przykładowego zlecenia {OrderId}.",
                id);

            throw;
        }
    }
}