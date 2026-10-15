using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using ProductionFlow.Api.Data;
using ProductionFlow.Api.Identity;

namespace ProductionFlow.Api.Authorization;

public sealed class ProductionAccessHandler
    : AuthorizationHandler<ProductionAccessRequirement>
{
    private readonly ProductionFlowDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProductionAccessHandler(
        ProductionFlowDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ProductionAccessRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (context.Resource is not HttpContext httpContext)
        {
            return;
        }

        // Ustalamy, która akcja kontrolera jest wykonywana.
        var descriptor = httpContext.GetEndpoint()?
            .Metadata.GetMetadata<ControllerActionDescriptor>();

        string? controller = descriptor?.ControllerName
            ?? httpContext.Request.RouteValues["controller"]?.ToString();

        string? action = descriptor?.ActionName
            ?? httpContext.Request.RouteValues["action"]?.ToString();

        if (controller is null || action is null)
        {
            return;
        }

        // Pobieramy role użytkownika z jego tożsamości.
        bool isAdmin = context.User.IsInRole(
            ProductionFlowRoles.Admin);

        bool isPlanner = context.User.IsInRole(
            ProductionFlowRoles.Planner);

        bool isOperator = context.User.IsInRole(
            ProductionFlowRoles.Operator);

        bool hasBusinessRole =
            isAdmin || isPlanner || isOperator;

        // Macierz uprawnień do endpointów.
        bool allowed = (controller, action) switch
        {
            // Uwierzytelniony użytkownik może sprawdzić
            // swoją sesję i się wylogować.
            ("Auth", "Me" or "Logout") => true,

            // Odczyt katalogu.
            ("Products" or "WorkCenters",
                "GetAll" or "GetById") => hasBusinessRole,

            // Edycja katalogu.
            ("Products" or "WorkCenters",
                "Create" or "Update") => isAdmin || isPlanner,

            // Zarządzanie zleceniami przez planistę.
            ("Orders",
                "GetAll" or "Create" or "Release" or "Cancel")
                => isAdmin || isPlanner,

            // Operacje produkcyjne.
            ("Orders",
                "Start" or "AddReport" or "Complete")
                => isAdmin || isOperator,

            // Odczyt pojedynczych zleceń.
            ("Orders",
                "GetById" or "GetReports" or
                "GetReportById" or "GetProgress")
                => hasBusinessRole,

            // Historia zleceń.
            ("OrderHistory", "GetHistory")
                => hasBusinessRole,

            // Zarządzanie przypisaniami operatorów.
            ("Operators",
                "GetAll" or "AssignWorkCenter")
                => isAdmin,

            // Własna lista zleceń operatora.
            ("OperatorOrders", "GetMine")
                => isOperator,

            // Pozostałe akcje nie są dozwolone.
            _ => false
        };

        if (!allowed)
        {
            return;
        }

        // Operator musi mieć dostęp do stanowiska
        // zlecenia, na którym chce wykonać operację.
        bool requiresWorkCenterCheck =
            (controller == "Orders" &&
             action is
                "GetById" or
                "GetReports" or
                "GetReportById" or
                "GetProgress" or
                "Start" or
                "AddReport" or
                "Complete")
            ||
            (controller == "OrderHistory" &&
             action == "GetHistory");

        // Administrator ma dostęp do wszystkich stanowisk.
        if (requiresWorkCenterCheck && isOperator && !isAdmin)
        {
            string routeParameter =
                controller == "OrderHistory" ? "orderId" : "id";

            string? idText = httpContext.Request
                .RouteValues[routeParameter]?.ToString();

            if (!Guid.TryParse(idText, out Guid orderId))
            {
                return;
            }

            var user = await _userManager.GetUserAsync(
                context.User);

            if (user?.AssignedWorkCenterId is not Guid workCenterId)
            {
                return;
            }

            bool hasAccess = await _db.ProductionOrders
                .AsNoTracking()
                .AnyAsync(
                    order =>
                        order.Id == orderId &&
                        order.WorkCenterId == workCenterId,
                    httpContext.RequestAborted);

            if (!hasAccess)
            {
                return;
            }
        }

        // Wszystkie wymagane sprawdzenia zakończyły się
        // pomyślnie.
        context.Succeed(requirement);
    }
}