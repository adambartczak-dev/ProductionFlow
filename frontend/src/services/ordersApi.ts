
import { ApiHttpError, getCsrfToken } from "./authApi";

import type {
    CreateProductionOrderRequest,
    OrderListQuery,
    PagedResult,
    ProductionOrderResponse
} from "../types";

const validStatuses = [
    "Draft",
    "Released",
    "InProgress",
    "Completed",
    "Cancelled"
];

// Sprawdzenie pojedynczego zlecenia.
function isOrder(
    value: unknown
): value is ProductionOrderResponse {
    if (
        typeof value !== "object" ||
        value === null
    ) {
        return false;
    }

    const order = value as Record<string, unknown>;

    return typeof order.id === "string" &&
        typeof order.number === "string" &&
        typeof order.productId === "string" &&
        typeof order.workCenterId === "string" &&
        Number.isSafeInteger(order.plannedQuantity) &&
        typeof order.dueDate === "string" &&
        typeof order.status === "string" &&
        validStatuses.includes(order.status);
}

// Sprawdzenie całej odpowiedzi stronicowanej.
function isPagedOrders(
    value: unknown
): value is PagedResult<ProductionOrderResponse> {
    if (
        typeof value !== "object" ||
        value === null
    ) {
        return false;
    }

    const page = value as Record<string, unknown>;

    return Array.isArray(page.items) &&
        page.items.every(isOrder) &&
        Number.isSafeInteger(page.page) &&
        Number(page.page) >= 1 &&
        Number.isSafeInteger(page.pageSize) &&
        Number(page.pageSize) >= 1 &&
        Number.isSafeInteger(page.totalCount) &&
        Number(page.totalCount) >= 0 &&
        Number.isSafeInteger(page.totalPages) &&
        Number(page.totalPages) >= 0;
}

// Pobieranie listy zleceń z API.
export async function getOrders(
    query: OrderListQuery,
    signal: AbortSignal
): Promise<PagedResult<ProductionOrderResponse>> {
    const params = new URLSearchParams({
        sortBy: query.sortBy,
        sortDirection: query.sortDirection,
        page: String(query.page),
        pageSize: String(query.pageSize)
    });

    if (query.search.trim()) {
        params.set("search", query.search.trim());
    }

    if (query.status) {
        params.set("status", query.status);
    }

    let response: Response;

    try {
        response = await fetch(
            `/api/orders?${params.toString()}`,
            {
                method: "GET",
                credentials: "same-origin",
                headers: {
                    Accept: "application/json"
                },
                cache: "no-store",
                signal
            }
        );
    }
    catch (error) {
        if (signal.aborted) {
            throw error;
        }

        throw new Error(
            "Nie można połączyć się z API. " +
            "Sprawdź backend.",
            { cause: error }
        );
    }

    if (response.status === 401) {
        throw new ApiHttpError(
            401,
            "Sesja wygasła. Zaloguj się ponownie."
        );
    }

    if (response.status === 403) {
        throw new ApiHttpError(
            403,
            "Nie masz uprawnień do listy zleceń."
        );
    }

    if (!response.ok) {
        throw new ApiHttpError(
            response.status,
            `Nie udało się pobrać zleceń ` +
            `(HTTP ${response.status}).`
        );
    }

    const data: unknown = await response.json();

    if (!isPagedOrders(data)) {
        throw new Error(
            "API zwróciło nieprawidłową " +
            "strukturę listy zleceń."
        );
    }

    return data;
}

// Wyciągamy czytelny komunikat z odpowiedzi ProblemDetails.
async function readProblemMessage(
    response: Response
): Promise<string | null> {
    try {
        const data: unknown = await response.json();

        if (typeof data !== "object" || data === null) {
            return null;
        }

        const problem = data as Record<string, unknown>;

        if (
            typeof problem.detail === "string" &&
            problem.detail.trim()
        ) {
            return problem.detail;
        }

        if (
            typeof problem.errors === "object" &&
            problem.errors !== null
        ) {
            const errors = Object.values(problem.errors);
            const firstMessage = errors.flat().find(
                item => typeof item === "string"
            );

            if (typeof firstMessage === "string") {
                return firstMessage;
            }
        }

        if (
            typeof problem.title === "string" &&
            problem.title.trim()
        ) {
            return problem.title;
        }
    }
    catch {
        // Nie każdy błąd HTTP zawiera JSON.
    }

    return null;
}

// Wysyłamy nowe zlecenie do backendu.
export async function createOrder(
    order: CreateProductionOrderRequest
): Promise<ProductionOrderResponse> {
    const csrfToken = await getCsrfToken();
    let response: Response;

    try {
        response = await fetch("/api/orders", {
            method: "POST",
            credentials: "same-origin",
            headers: {
                Accept: "application/json",
                "Content-Type": "application/json",
                "X-CSRF-TOKEN": csrfToken
            },
            body: JSON.stringify(order)
        });
    }
    catch (error) {
        throw new Error(
            "Nie można wysłać zlecenia do API. " +
            "Sprawdź połączenie i listę zleceń przed ponowną próbą.",
            { cause: error }
        );
    }

    if (response.status === 401) {
        throw new ApiHttpError(
            401,
            "Sesja wygasła. Zaloguj się ponownie."
        );
    }

    if (response.status === 403) {
        throw new ApiHttpError(
            403,
            "Nie masz uprawnień do tworzenia zleceń."
        );
    }

    if (!response.ok) {
        const details = await readProblemMessage(response);

        throw new ApiHttpError(
            response.status,
            details ??
            `Nie udało się utworzyć zlecenia (HTTP ${response.status}).`
        );
    }

    const data: unknown = await response.json();

    if (!isOrder(data)) {
        throw new Error(
            "Zlecenie mogło zostać zapisane, ale API zwróciło " +
            "nieprawidłową odpowiedź. Sprawdź listę zleceń."
        );
    }

    return data;
}
