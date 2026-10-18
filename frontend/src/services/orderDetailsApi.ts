
import { ApiHttpError, getCsrfToken } from "./authApi";
import type {
    CreateExecutionReportRequest,
    ExecutionReportResponse,
    OrderHistoryResponse,
    ProductionOrderResponse,
    ProductionProgressResponse
} from "../types";

const statuses = [
    "Draft",
    "Released",
    "InProgress",
    "Completed",
    "Cancelled"
];

function isObject(
    value: unknown
): value is Record<string, unknown> {
    return typeof value === "object" && value !== null;
}

function isOrder(
    value: unknown
): value is ProductionOrderResponse {
    if (!isObject(value)) return false;

    return typeof value.id === "string" &&
        typeof value.number === "string" &&
        typeof value.productId === "string" &&
        typeof value.workCenterId === "string" &&
        Number.isInteger(value.plannedQuantity) &&
        typeof value.dueDate === "string" &&
        typeof value.status === "string" &&
        statuses.includes(value.status);
}

function isProgress(
    value: unknown
): value is ProductionProgressResponse {
    if (!isObject(value)) return false;

    return typeof value.id === "string" &&
        typeof value.number === "string" &&
        typeof value.status === "string" &&
        statuses.includes(value.status) &&
        [
            "plannedQuantity",
            "goodQuantity",
            "scrapQuantity",
            "totalQuantity",
            "remainingQuantity",
            "progressPercent",
            "reportsCount"
        ].every(
            key => typeof value[key] === "number" &&
                Number.isFinite(value[key])
        );
}

function isReport(
    value: unknown
): value is ExecutionReportResponse {
    if (!isObject(value)) return false;

    return [
        "id",
        "productionOrderId",
        "operatorId",
        "createdAtUtc"
    ].every(key => typeof value[key] === "string") &&
        [
            "goodQuantity",
            "scrapQuantity",
            "totalQuantity"
        ].every(
            key => typeof value[key] === "number" &&
                Number.isFinite(value[key])
        );
}

function isHistory(
    value: unknown
): value is OrderHistoryResponse {
    if (!isObject(value)) return false;

    return [
        "id",
        "productionOrderId",
        "eventType",
        "details",
        "occurredAtUtc"
    ].every(key => typeof value[key] === "string") &&
        (
            value.actor === null ||
            typeof value.actor === "string"
        );
}

async function readError(
    response: Response
): Promise<string> {
    try {
        const data: unknown = await response.json();

        if (isObject(data)) {
            if (
                typeof data.detail === "string" &&
                data.detail
            ) {
                return data.detail;
            }

            if (
                typeof data.title === "string" &&
                data.title
            ) {
                return data.title;
            }
        }
    }
    catch {
        // Błąd nie musi zawierać JSON.
    }

    return `Błąd żądania (HTTP ${response.status}).`;
}

// Wspólna obsługa żądań GET i POST raportu.
async function request(
    path: string,
    signal?: AbortSignal,
    body?: CreateExecutionReportRequest
): Promise<unknown> {
    const method = body ? "POST" : "GET";
    const csrfToken = body ? await getCsrfToken() : null;

    let response: Response;

    try {
        response = await fetch(path, {
            method,
            credentials: "same-origin",
            cache: "no-store",
            headers: {
                Accept: "application/json",
                ...(body ? {
                    "Content-Type": "application/json",
                    "X-CSRF-TOKEN": csrfToken!
                } : {})
            },
            ...(body
                ? { body: JSON.stringify(body) }
                : {}),
            signal
        });
    }
    catch (error) {
        if (signal?.aborted) throw error;

        throw new Error(
            method === "POST"
                ? "Brak odpowiedzi z API. Przed ponowieniem sprawdź raporty."
                : "Nie można połączyć się z API.",
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
            "Brak uprawnień do tej operacji."
        );
    }

    if (!response.ok) {
        throw new ApiHttpError(
            response.status,
            await readError(response)
        );
    }

    return response.json() as Promise<unknown>;
}

// Pobranie szczegółów zlecenia.
export async function getOrder(
    id: string,
    signal: AbortSignal
) {
    const data = await request(`/api/orders/${id}`, signal);

    if (!isOrder(data)) {
        throw new Error("Nieprawidłowe dane zlecenia.");
    }

    return data;
}

// Pobranie postępu realizacji.
export async function getProgress(
    id: string,
    signal: AbortSignal
) {
    const data = await request(
        `/api/orders/${id}/progress`,
        signal
    );

    if (!isProgress(data)) {
        throw new Error("Nieprawidłowy postęp zlecenia.");
    }

    return data;
}

// Pobranie historii zdarzeń.
export async function getHistory(
    id: string,
    signal: AbortSignal
) {
    const data = await request(
        `/api/orders/${id}/history`,
        signal
    );

    if (
        !Array.isArray(data) ||
        !data.every(isHistory)
    ) {
        throw new Error(
            "Nieprawidłowa historia zlecenia."
        );
    }

    return data as OrderHistoryResponse[];
}

// Pobranie zapisanych raportów.
export async function getReports(
    id: string,
    signal: AbortSignal
) {
    const data = await request(
        `/api/orders/${id}/reports`,
        signal
    );

    if (
        !Array.isArray(data) ||
        !data.every(isReport)
    ) {
        throw new Error(
            "Nieprawidłowa lista raportów."
        );
    }

    return data as ExecutionReportResponse[];
}

// Pobranie zleceń przypisanych do operatora.
export async function getMyOrders(
    signal: AbortSignal
) {
    const data = await request(
        "/api/operator/orders",
        signal
    );

    if (
        !Array.isArray(data) ||
        !data.every(isOrder)
    ) {
        throw new Error(
            "Nieprawidłowa lista zleceń operatora."
        );
    }

    return data as ProductionOrderResponse[];
}

// Zmiana statusu zlecenia.
export async function changeOrderStatus(
    id: string,
    operation: "release" | "start" | "complete"
): Promise<void> {
    const csrfToken = await getCsrfToken();
    let response: Response;

    try {
        response = await fetch(
            `/api/orders/${id}/${operation}`,
            {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "X-CSRF-TOKEN": csrfToken
                }
            }
        );
    }
    catch (error) {
        throw new Error(
            "Nie można potwierdzić zmiany statusu. " +
            "Odśwież szczegóły przed ponowieniem.",
            { cause: error }
        );
    }

    if (response.status === 401) {
        throw new ApiHttpError(
            401,
            "Sesja wygasła."
        );
    }

    if (response.status === 403) {
        throw new ApiHttpError(
            403,
            "Brak uprawnień."
        );
    }

    if (!response.ok) {
        throw new ApiHttpError(
            response.status,
            await readError(response)
        );
    }
}

// Dodawanie rzeczywistego raportu produkcji.
export async function addExecutionReport(
    id: string,
    report: CreateExecutionReportRequest
): Promise<void> {
    const data = await request(
        `/api/orders/${id}/reports`,
        undefined,
        report
    );

    if (!isReport(data)) {
        throw new Error(
            "Zapis mógł się udać; sprawdź listę " +
            "raportów przed ponowieniem."
        );
    }
}
