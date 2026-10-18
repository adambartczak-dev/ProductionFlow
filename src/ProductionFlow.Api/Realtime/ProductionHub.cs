
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using ProductionFlow.Api.Identity;

namespace ProductionFlow.Api.Realtime;

// Nazwy grup są wykorzystywane przez hub i nadawcę zdarzeń.
public static class ProductionHubGroups
{
    public const string Planning = "production:planning";

    public static string ForWorkCenter(Guid workCenterId)
    {
        return $"production:workcenter:{workCenterId:N}";
    }
}

[Authorize(Policy = "ProductionRealtime")]
public sealed class ProductionHub : Hub
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ProductionHub> _logger;

    public ProductionHub(
        UserManager<ApplicationUser> userManager,
        ILogger<ProductionHub> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var principal = Context.User;

        if (principal is null)
        {
            Context.Abort();
            return;
        }

        bool isAdmin = principal.IsInRole(
            ProductionFlowRoles.Admin);

        bool isPlanner = principal.IsInRole(
            ProductionFlowRoles.Planner);

        bool isOperator = principal.IsInRole(
            ProductionFlowRoles.Operator);

        // Administrator i planista obserwują wszystkie zlecenia.
        if (isAdmin || isPlanner)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                ProductionHubGroups.Planning);
        }

        // Operator otrzymuje zdarzenia tylko dla
        // stanowiska przypisanego do jego konta.
        if (isOperator)
        {
            var user = await _userManager.GetUserAsync(
                principal);

            if (user?.AssignedWorkCenterId is Guid workCenterId)
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    ProductionHubGroups.ForWorkCenter(
                        workCenterId));
            }
        }

        _logger.LogInformation(
            "SignalR: połączono klienta {ConnectionId}",
            Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(
        Exception? exception)
    {
        _logger.LogInformation(
            "SignalR: rozłączono klienta {ConnectionId}",
            Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }
}
