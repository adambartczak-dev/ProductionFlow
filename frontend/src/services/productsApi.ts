
import type { ProductResponse } from "../types";
import { ApiHttpError } from "./authApi";

// Sprawdza strukturę produktu.
function isProductResponse(
    value: unknown
): value is ProductResponse {
    if (
        typeof value !== "object" ||
        value === null
    ) {
        return false;
    }

    const product =
        value as Record<string, unknown>;

    return typeof product.id === "string" &&
        typeof product.code === "string" &&
        typeof product.name === "string";
}

// Pobranie produktów z REST API.
export async function getProducts(
    signal: AbortSignal
): Promise<ProductResponse[]> {
    let response: Response;

    try {
        response = await fetch("/api/products", {
            method: "GET",
            headers: {
                Accept: "application/json"
            },
            credentials: "same-origin",
            signal
        });
    }
    catch (error) {
        if (signal.aborted) {
            throw error;
        }

        throw new Error(
            "Nie można połączyć się z API. " +
            "Sprawdź, czy backend działa.",
            { cause: error }
        );
    }

    // Sesja wygasła.
    if (response.status === 401) {
        throw new ApiHttpError(
            401,
            "Sesja wygasła. Zaloguj się ponownie."
        );
    }

    // Brak wystarczających uprawnień.
    if (response.status === 403) {
        throw new ApiHttpError(
            403,
            "Brak uprawnień do katalogu produktów."
        );
    }

    // Pozostałe błędy HTTP.
    if (!response.ok) {
        throw new ApiHttpError(
            response.status,
            "Błąd serwera przy pobieraniu produktów " +
            `(HTTP ${response.status}).`
        );
    }

    const data: unknown = await response.json();

    if (
        !Array.isArray(data) ||
        !data.every(isProductResponse)
    ) {
        throw new Error(
            "Odpowiedź API ma nieprawidłową strukturę."
        );
    }

    return data;
}
