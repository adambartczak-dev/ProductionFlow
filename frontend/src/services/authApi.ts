
import type { AuthUser } from "../types";

// Błąd zawierający status odpowiedzi HTTP.
export class ApiHttpError extends Error {
    readonly status: number;

    constructor(status: number, message: string) {
        super(message);
        this.name = "ApiHttpError";
        this.status = status;
    }
}

// Sprawdzenie odpowiedzi zawierającej dane użytkownika.
function parseUser(data: unknown): AuthUser {
    if (typeof data !== "object" || data === null) {
        throw new Error("Nieprawidłowa odpowiedź serwera.");
    }

    const value = data as Record<string, unknown>;

    if (
        typeof value.id !== "string" ||
        typeof value.email !== "string" ||
        typeof value.displayName !== "string" ||
        !Array.isArray(value.roles) ||
        !value.roles.every((role) => typeof role === "string")
    ) {
        throw new Error("Nieprawidłowe dane użytkownika.");
    }

    return {
        id: value.id,
        email: value.email,
        displayName: value.displayName,
        roles: value.roles as string[]
    };
}

// Pobranie tokenu CSRF.
export async function getCsrfToken(): Promise<string> {
    const response = await fetch("/api/auth/csrf-token", {
        credentials: "same-origin",
        cache: "no-store"
    });

    if (!response.ok) {
        throw new ApiHttpError(
            response.status,
            "Nie można pobrać tokenu CSRF."
        );
    }

    const data: unknown = await response.json();

    if (
        typeof data !== "object" ||
        data === null ||
        !("requestToken" in data) ||
        typeof data.requestToken !== "string"
    ) {
        throw new Error(
            "Odpowiedź CSRF ma nieprawidłowy format."
        );
    }

    return data.requestToken;
}

// Odtworzenie zalogowanego użytkownika.
export async function getCurrentUser():
    Promise<AuthUser | null> {
    const response = await fetch("/api/auth/me", {
        credentials: "same-origin",
        cache: "no-store"
    });

    if (response.status === 401) {
        return null;
    }

    if (!response.ok) {
        throw new ApiHttpError(
            response.status,
            `Nie udało się sprawdzić sesji (HTTP ${response.status}).`
        );
    }

    return parseUser(await response.json());
}

// Logowanie za pomocą Identity.
export async function login(
    email: string,
    password: string
): Promise<AuthUser> {
    const csrfToken = await getCsrfToken();

    const response = await fetch("/api/auth/login", {
        method: "POST",
        credentials: "same-origin",
        headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrfToken
        },
        body: JSON.stringify({ email, password })
    });

    if (response.status === 401) {
        throw new ApiHttpError(
            401,
            "Nieprawidłowy adres e-mail lub hasło."
        );
    }

    if (response.status === 429) {
        throw new ApiHttpError(
            429,
            "Zbyt wiele prób logowania. Spróbuj ponownie później."
        );
    }

    if (!response.ok) {
        throw new ApiHttpError(
            response.status,
            `Logowanie nie powiodło się (HTTP ${response.status}).`
        );
    }

    return parseUser(await response.json());
}

// Wylogowanie aktualnego użytkownika.
export async function logout(): Promise<void> {
    // Token pobieramy ponownie już w zalogowanej sesji.
    const csrfToken = await getCsrfToken();

    const response = await fetch("/api/auth/logout", {
        method: "POST",
        credentials: "same-origin",
        headers: {
            "X-CSRF-TOKEN": csrfToken
        }
    });

    // Sesja mogła już wygasnąć.
    if (response.status === 401) {
        return;
    }

    if (!response.ok) {
        throw new ApiHttpError(
            response.status,
            `Wylogowanie nie powiodło się (HTTP ${response.status}).`
        );
    }
}
