
export type ProductionOrderStatus =
    | "Draft"
    | "Released"
    | "InProgress"
    | "Completed"
    | "Cancelled";

export interface DemoProduct {
    code: string;
    name: string;
}

export interface ProductResponse {
    id: string;
    code: string;
    name: string;
}

export interface OrderOverview {
    number: string;
    productCode: string;
    plannedQuantity: number;
    goodQuantity: number;
    scrapQuantity: number;
    status: ProductionOrderStatus;
}

export interface AuthUser {
    id: string;
    email: string;
    displayName: string;
    roles: string[];
}

export interface ProductionOrderResponse {
    id: string;
    number: string;
    productId: string;
    workCenterId: string;
    plannedQuantity: number;
    dueDate: string;
    status: ProductionOrderStatus;
}

export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
}

export type OrderSortBy =
    | "number"
    | "dueDate"
    | "plannedQuantity"
    | "status";

export interface OrderListQuery {
    search: string;
    status: ProductionOrderStatus | "";
    sortBy: OrderSortBy;
    sortDirection: "asc" | "desc";
    page: number;
    pageSize: number;
}

export interface WorkCenterResponse {
    id: string;
    code: string;
    name: string;
}

export interface CreateProductionOrderRequest {
    number: string;
    productId: string;
    workCenterId: string;
    plannedQuantity: number;
    dueDate: string;
}

// Dane pojedynczego zapisanego raportu.
export interface ExecutionReportResponse {
    id: string;
    productionOrderId: string;
    operatorId: string;
    goodQuantity: number;
    scrapQuantity: number;
    totalQuantity: number;
    createdAtUtc: string;
}

// Podsumowanie realizacji zlecenia.
export interface ProductionProgressResponse {
    id: string;
    number: string;
    status: ProductionOrderStatus;
    plannedQuantity: number;
    goodQuantity: number;
    scrapQuantity: number;
    totalQuantity: number;
    remainingQuantity: number;
    progressPercent: number;
    reportsCount: number;
}

// Zdarzenie zapisane w historii.
export interface OrderHistoryResponse {
    id: string;
    productionOrderId: string;
    eventType: string;
    details: string;
    actor: string | null;
    occurredAtUtc: string;
}

// Dane nowego raportu wysyłanego do API.
export interface CreateExecutionReportRequest {
    operatorId: string;
    goodQuantity: number;
    scrapQuantity: number;
}
