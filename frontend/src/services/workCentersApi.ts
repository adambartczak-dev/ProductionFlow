
import type { WorkCenterResponse } from "../types";
import { ApiHttpError } from "./authApi";

// Sprawdzamy dane zwracane przez backend.
function isWorkCenter(
    value: unknown
): value is WorkCenterResponse {
    if (typeof value !== "object" || value === null) {
        return false;
    }

    const item = value as Record<string, unknown>;

    return typeof item.id === "string" &&
        typeof item.code === "string" &&
        typeof item.name === "string";
}

// Pobieramy listę stanowisk produkcyjnych.
export async function getWorkCenters(
    signal: AbortSignal
): Promise<WorkCenterResponse[]> {
    let response: Response;

    try {
        response = await fetch("/api/workcenters", {
            method: "GET",
            credentials: "same-origin",
            headers: {
                Accept: "application/json"
            },
            signal
        });
    }
    catch (error) {
        if (signal.aborted) {
            throw error;
        }

        throw new Error(
            "Nie można połączyć się z API stanowisk.",
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
            "Brak uprawnień do listy stanowisk."
        );
    }

    if (!response.ok) {
        throw new ApiHttpError(
            response.status,
            `Nie udało się pobrać stanowisk (HTTP ${response.status}).`
        );
    }

    const data: unknown = await response.json();

    if (!Array.isArray(data) || !data.every(isWorkCenter)) {
        throw new Error(
            "API zwróciło nieprawidłową listę stanowisk."
        );
    }

    return data;
}
