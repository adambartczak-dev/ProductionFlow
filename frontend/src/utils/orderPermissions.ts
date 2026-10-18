
import type {
    AuthUser,
    ProductionOrderStatus
} from "../types";

// UI odzwierciedla role. Autoryzacja faktycznie działa w API.
export function canPlanOrders(user: AuthUser): boolean {
    return user.roles.includes("Admin") || user.roles.includes("Planner");
}

export function canExecuteOrders(user: AuthUser): boolean {
    return user.roles.includes("Admin") || user.roles.includes("Operator");
}

export function canUseOperatorList(user: AuthUser): boolean {
    // Endpoint /api/operator/orders jest dostępny dla roli Operator.
    return user.roles.includes("Operator");
}

export function canReadOrder(user: AuthUser): boolean {
    return canPlanOrders(user) || canExecuteOrders(user);
}

export type OrderAction = "release" | "start" | "report" | "complete";

export function canPerformOrderAction(
    user: AuthUser,
    status: ProductionOrderStatus,
    remainingQuantity: number,
    action: OrderAction
): boolean {
    switch (action) {
        case "release":
            return canPlanOrders(user) && status === "Draft";
        case "start":
            return canExecuteOrders(user) && status === "Released";
        case "report":
            return canExecuteOrders(user) &&
                status === "InProgress" && remainingQuantity > 0;
        case "complete":
            return canExecuteOrders(user) &&
                status === "InProgress" && remainingQuantity === 0;
    }
}
