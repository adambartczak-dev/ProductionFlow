
namespace ProductionFlow.Api.Identity;

public static class ProductionFlowRoles
{
    public const string Admin = "Admin";
    public const string Planner = "Planner";
    public const string Operator = "Operator";

    public static readonly string[] All =
    [
        Admin,
        Planner,
        Operator
    ];
}
