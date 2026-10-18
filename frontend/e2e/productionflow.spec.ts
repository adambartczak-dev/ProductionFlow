
import { expect, test } from "@playwright/test";
import type { Page } from "@playwright/test";

// Role użytkowników występujące w ProductionFlow.
type TestRole = "Admin" | "Planner" | "Operator";

// Konta demonstracyjne i nazwy zmiennych środowiskowych.
const accounts = {
    Admin: {
        email: "admin@productionflow.local",
        passwordKey: "PF_E2E_ADMIN_PASSWORD"
    },
    Planner: {
        email: "planner@productionflow.local",
        passwordKey: "PF_E2E_PLANNER_PASSWORD"
    },
    Operator: {
        email: "operator@productionflow.local",
        passwordKey: "PF_E2E_OPERATOR_PASSWORD"
    }
} as const;

// Odczyt hasła z lokalnej konfiguracji.
function getPassword(role: TestRole): string {
    const key = accounts[role].passwordKey;
    const password = process.env[key];

    if (
        !password ||
        password.startsWith("UZUPELNIJ_")
    ) {
        throw new Error(
            `Brak poprawnej wartości ${key}. ` +
            "Uzupełnij frontend/.env.e2e.local."
        );
    }

    return password;
}

// Logowanie przez rzeczywisty formularz React.
async function loginAs(
    page: Page,
    role: TestRole
): Promise<void> {
    const account = accounts[role];
    const password = getPassword(role);

    await page.goto("/login");

    await page.getByLabel("Adres e-mail").fill(
        account.email
    );

    await page.getByLabel("Hasło").fill(password);

    await page.getByRole("button", {
        name: "Zaloguj się"
    }).click();

    // Po zalogowaniu React przenosi nas do panelu.
    await expect(page).toHaveURL(/\/$/);

    await expect(
        page.getByRole("heading", {
            name: /^Witaj,/
        })
    ).toBeVisible();
}

// Każdy test otrzymuje inny numer zlecenia.
function createUniqueOrderNumber(): string {
    const timestamp = Date.now().toString(36);
    const randomPart = Math.random()
        .toString(36)
        .slice(2, 8);

    return `E2E-D34-${timestamp}-${randomPart}`;
}

// Wypełnienie formularza nowego zlecenia.
async function fillNewOrderForm(
    page: Page,
    number: string,
    quantity: number
): Promise<void> {
    await page.goto("/orders/new");

    await expect(
        page.getByRole("heading", {
            name: "Nowe zlecenie"
        })
    ).toBeVisible();

    await page.getByLabel(
        "Numer zlecenia"
    ).fill(number);

    // Wybieramy pierwszy rzeczywisty produkt z API.
    await page.getByLabel(
        "Produkt",
        { exact: true }
    ).selectOption({ index: 1 });

    // Wybieramy pierwsze rzeczywiste stanowisko z API.
    await page.getByLabel(
        "Stanowisko produkcyjne"
    ).selectOption({ index: 1 });

    await page.getByLabel(
        "Planowana ilość"
    ).fill(String(quantity));

    // Termin siedem dni od aktualnej daty.
    const dueDate = new Date(
        Date.now() + 7 * 24 * 60 * 60 * 1000
    ).toISOString().slice(0, 10);

    await page.getByLabel(
        "Termin realizacji"
    ).fill(dueDate);
}

// Kliknięcie przycisku i sprawdzenie zapisu.
async function submitNewOrder(
    page: Page
): Promise<void> {
    await page.getByRole("button", {
        name: "Utwórz zlecenie"
    }).click();

    await expect(
        page.getByRole("heading", {
            name: "Zlecenie zostało utworzone"
        })
    ).toBeVisible();
}

// Sprawdzenie, czy backend jest uruchomiony.
test.beforeEach(async ({ request }) => {
    const response = await request.get(
        "/api/auth/csrf-token"
    );

    expect(
        response.status(),
        "API powinno działać na localhost:5041. " +
        "Uruchom ProductionFlow.Api przed testami."
    ).toBe(200);
});

// TEST 1 — ochrona tras niezalogowanego użytkownika.
test(
    "01 - niezalogowany użytkownik trafia do logowania",
    async ({ page, request }) => {
        await page.goto("/orders/new");

        await expect(page).toHaveURL(/\/login$/);

        await expect(
            page.getByRole("heading", {
                name: "Logowanie"
            })
        ).toBeVisible();

        // Sprawdzamy także ochronę REST API.
        const response = await request.get(
            "/api/orders?sortBy=dueDate" +
            "&sortDirection=asc&page=1&pageSize=10"
        );

        expect(response.status()).toBe(401);
    }
);

// TEST 2 — planista tworzy zlecenie i widzi konflikt.
test(
    "02 - planista tworzy zlecenie i nie może powtórzyć numeru",
    async ({ page }) => {
        await loginAs(page, "Planner");

        // Planista powinien widzieć funkcje planowania.
        await expect(
            page.getByRole("link", {
                name: "Nowe zlecenie",
                exact: true
            })
        ).toBeVisible();

        const orderNumber = createUniqueOrderNumber();

        await fillNewOrderForm(
            page,
            orderNumber,
            100
        );

        await submitNewOrder(page);

        // Próbujemy zapisać drugi raz ten sam numer.
        await fillNewOrderForm(
            page,
            orderNumber,
            100
        );

        await page.getByRole("button", {
            name: "Utwórz zlecenie"
        }).click();

        // Backend powinien zwrócić 409 Conflict,
        // a React pokaże komunikat o istniejącym numerze.
        await expect(
            page.getByRole("alert")
        ).toContainText(/istnieje/i);
    }
);

// TEST 3 — pełny proces produkcyjny administratora.
test(
    "03 - administrator realizuje całe zlecenie",
    async ({ page }) => {
        await loginAs(page, "Admin");

        const orderNumber = createUniqueOrderNumber();

        // Utworzenie zlecenia na 20 sztuk.
        await fillNewOrderForm(
            page,
            orderNumber,
            20
        );

        await submitNewOrder(page);

        // Przejście do listy zleceń.
        await page.getByRole("link", {
            name: "Przejdź do listy zleceń"
        }).click();

        // Wyszukanie utworzonego zlecenia.
        await page.getByLabel(
            "Numer zlecenia"
        ).fill(orderNumber);

        await page.getByRole("button", {
            name: "Szukaj"
        }).click();

        const orderLink = page.getByRole("link", {
            name: orderNumber,
            exact: true
        });

        await expect(orderLink).toBeVisible();
        await orderLink.click();

        await expect(
            page.getByRole("heading", {
                name: orderNumber,
                exact: true
            })
        ).toBeVisible();

        // Draft -> Released.
        await page.getByRole("button", {
            name: "Wydaj do produkcji"
        }).click();

        await expect(
            page.getByRole("button", {
                name: "Rozpocznij realizację"
            })
        ).toBeEnabled();

        // Released -> InProgress.
        await page.getByRole("button", {
            name: "Rozpocznij realizację"
        }).click();

        await expect(
            page.getByRole("button", {
                name: "Zapisz raport"
            })
        ).toBeEnabled();

        // Raport: 15 dobrych sztuk, 5 braków.
        await page.getByLabel(
            "Dobre sztuki"
        ).fill("15");

        await page.getByLabel(
            "Braki"
        ).fill("5");

        await page.getByRole("button", {
            name: "Zapisz raport"
        }).click();

        // Postęp powinien wynosić 100%.
        await expect(
            page.getByRole("progressbar")
        ).toHaveAttribute("aria-valuenow", "100");

        // Weryfikacja zapisanego raportu.
        await expect(
            page.getByRole("heading", {
                name: /Raporty wykonania \(1\)/
            })
        ).toBeVisible();

        // InProgress -> Completed.
        await page.getByRole("button", {
            name: "Zakończ zlecenie"
        }).click();

        await expect(
            page.getByText(
                "Zakończone",
                { exact: true }
            )
        ).toBeVisible();

        // Kontrola widoczności historii zmian.
        await expect(
            page.getByRole("heading", {
                name: /Historia zmian/
            })
        ).toBeVisible();

        // Odświeżenie całej strony przeglądarki.
        await page.reload();

        // Stan Completed musi przetrwać odświeżenie.
        await expect(
            page.getByText(
                "Zakończone",
                { exact: true }
            )
        ).toBeVisible();
    }
);

// TEST 4 — operator ma ograniczony interfejs.
test(
    "04 - operator widzi tylko dozwolone funkcje",
    async ({ page }) => {
        await loginAs(page, "Operator");

        // Operator widzi własną listę.
        await expect(
            page.getByRole("link", {
                name: "Moje zlecenia",
                exact: true
            })
        ).toBeVisible();

        // Nie powinien widzieć tworzenia zleceń.
        await expect(
            page.getByRole("link", {
                name: "Nowe zlecenie",
                exact: true
            })
        ).toHaveCount(0);

        // Sprawdzamy ręczne wpisanie adresu URL.
        await page.goto("/orders/new");

        await expect(
            page.getByText(
                "Brak uprawnień do tworzenia zleceń."
            )
        ).toBeVisible();

        // Operator nie ma globalnej listy zleceń.
        await page.goto("/orders");

        await expect(
            page.getByText(
                "Brak uprawnień do globalnej listy zleceń."
            )
        ).toBeVisible();

        // Własna lista nadal jest dostępna.
        await page.goto("/operator/orders");

        await expect(
            page.getByRole("heading", {
                name: "Moje zlecenia"
            })
        ).toBeVisible();
    }
);
