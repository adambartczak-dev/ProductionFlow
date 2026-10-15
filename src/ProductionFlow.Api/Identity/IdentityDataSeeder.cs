
using Microsoft.AspNetCore.Identity;

namespace ProductionFlow.Api.Identity;

public static class IdentityDataSeeder
{
    public static async Task SeedDevelopmentAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        // 1. Tworzenie trzech ról.
        foreach (var roleName in ProductionFlowRoles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var role = new IdentityRole<Guid>(roleName)
            {
                Id = Guid.NewGuid()
            };

            var result = await roleManager.CreateAsync(role);

            EnsureSucceeded(
                result,
                $"utworzyć rolę {roleName}");
        }

        // 2. Definicje przykładowych użytkowników.
        var demoUsers = new[]
        {
            new
            {
                Role = ProductionFlowRoles.Admin,
                Email = "admin@productionflow.local",
                DisplayName = "Administrator Demo"
            },
            new
            {
                Role = ProductionFlowRoles.Planner,
                Email = "planner@productionflow.local",
                DisplayName = "Planista Demo"
            },
            new
            {
                Role = ProductionFlowRoles.Operator,
                Email = "operator@productionflow.local",
                DisplayName = "Operator Demo"
            }
        };

        // 3. Tworzenie użytkowników i przypisywanie ról.
        foreach (var demo in demoUsers)
        {
            var password = configuration[
                $"DemoUsers:{demo.Role}:Password"];

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    $"Brak hasła DemoUsers:{demo.Role}:Password " +
                    "w User Secrets.");
            }

            var user = await userManager
                .FindByEmailAsync(demo.Email);

            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = demo.Email,
                    Email = demo.Email,
                    DisplayName = demo.DisplayName,
                    EmailConfirmed = true
                };

                var createResult = await userManager
                    .CreateAsync(user, password);

                EnsureSucceeded(
                    createResult,
                    $"utworzyć użytkownika {demo.Email}");
            }

            if (!await userManager.IsInRoleAsync(
                    user, demo.Role))
            {
                var roleResult = await userManager
                    .AddToRoleAsync(user, demo.Role);

                EnsureSucceeded(
                    roleResult,
                    $"przypisać rolę {demo.Role} " +
                    $"użytkownikowi {demo.Email}");
            }
        }
    }

    private static void EnsureSucceeded(
        IdentityResult result,
        string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(
            "; ",
            result.Errors.Select(error => error.Description));

        throw new InvalidOperationException(
            $"Nie udało się {operation}. {errors}");
    }
}
