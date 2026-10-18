
using Microsoft.EntityFrameworkCore;
using ProductionFlow.Api.Domain;

namespace ProductionFlow.Api.Data;

public static class DevelopmentCatalogSeeder
{
    public static async Task SeedAsync(
        ProductionFlowDbContext db)
    {
        // Dodajemy produkty wyłącznie do pustego katalogu.
        if (!await db.Products.AnyAsync())
        {
            db.Products.AddRange(
                new Product(
                    "PRD-100",
                    "Element montażowy A"),
                new Product(
                    "PRD-200",
                    "Element montażowy B"));
        }

        // Dodajemy stanowiska wyłącznie do pustego katalogu.
        if (!await db.WorkCenters.AnyAsync())
        {
            db.WorkCenters.AddRange(
                new WorkCenter(
                    "WC-01",
                    "Stanowisko montażowe"),
                new WorkCenter(
                    "WC-02",
                    "Stanowisko pakowania"));
        }

        await db.SaveChangesAsync();
    }
}
