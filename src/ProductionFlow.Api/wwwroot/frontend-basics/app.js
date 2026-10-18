
"use strict";

// 1. Import funkcji z osobnego modułu.
import { fetchDemoProducts } from "./productsApi.js";

// 2. Dane przykładowego zlecenia.
const plannedQuantity = 100;

let goodQuantity = 0;
let scrapQuantity = 0;
let orderStatus = "InProgress";

// 3. Pobieranie elementów HTML.
const plannedElement =
    document.getElementById("planned-quantity");

const goodElement =
    document.getElementById("good-quantity");

const scrapElement =
    document.getElementById("scrap-quantity");

const progressElement =
    document.getElementById("progress-percent");

const progressBar =
    document.getElementById("progress-bar");

const remainingElement =
    document.getElementById("remaining-quantity");

const statusElement =
    document.getElementById("order-status");

const messageElement =
    document.getElementById("message");

const reportForm =
    document.getElementById("report-form");

const goodInput =
    document.getElementById("good-input");

const scrapInput =
    document.getElementById("scrap-input");

const reportButton =
    document.getElementById("report-button");

const completeButton =
    document.getElementById("complete-button");

const resetButton =
    document.getElementById("reset-button");

// Nowe elementy sekcji produktów.
const loadProductsButton =
    document.getElementById("load-products-button");

const productsMessage =
    document.getElementById("products-message");

const productsList =
    document.getElementById("products-list");

// 4. Wyświetlanie komunikatów zlecenia.
function showMessage(text, isError = false) {
    messageElement.textContent = text;

    messageElement.classList.toggle(
        "message--error",
        isError
    );
}

// 5. Aktualizacja danych zlecenia.
function updateView() {
    const totalQuantity =
        goodQuantity + scrapQuantity;

    const remainingQuantity =
        plannedQuantity - totalQuantity;

    const progress =
        (totalQuantity / plannedQuantity) * 100;

    plannedElement.textContent = plannedQuantity;
    goodElement.textContent = goodQuantity;
    scrapElement.textContent = scrapQuantity;

    progressElement.textContent = `${progress}%`;
    progressBar.style.width = `${progress}%`;

    progressBar.parentElement.setAttribute(
        "aria-valuenow",
        String(progress)
    );

    remainingElement.textContent =
        `Pozostało ${remainingQuantity} sztuk do rozliczenia.`;

    const isCompleted = orderStatus === "Completed";

    statusElement.textContent = isCompleted
        ? "Zakończone"
        : "W realizacji";

    statusElement.classList.toggle(
        "status-badge--complete",
        isCompleted
    );

    const canReport =
        !isCompleted && remainingQuantity > 0;

    reportButton.disabled = !canReport;
    goodInput.disabled = !canReport;
    scrapInput.disabled = !canReport;

    completeButton.disabled =
        isCompleted || remainingQuantity !== 0;
}

// 6. Obsługa formularza raportowania.
reportForm.addEventListener("submit", function (event) {
    event.preventDefault();

    const newGood = Number(goodInput.value);
    const newScrap = Number(scrapInput.value);

    if (
        !Number.isSafeInteger(newGood) ||
        !Number.isSafeInteger(newScrap) ||
        newGood < 0 ||
        newScrap < 0
    ) {
        showMessage(
            "Podaj poprawne, nieujemne liczby całkowite.",
            true
        );
        return;
    }

    const reportTotal = newGood + newScrap;

    if (reportTotal === 0) {
        showMessage(
            "Raport musi zawierać co najmniej jedną sztukę.",
            true
        );
        return;
    }

    const remainingQuantity =
        plannedQuantity - goodQuantity - scrapQuantity;

    if (reportTotal > remainingQuantity) {
        showMessage(
            "Raport przekracza pozostałą ilość do wykonania.",
            true
        );
        return;
    }

    goodQuantity += newGood;
    scrapQuantity += newScrap;

    goodInput.value = "0";
    scrapInput.value = "0";

    updateView();

    const totalQuantity =
        goodQuantity + scrapQuantity;

    if (totalQuantity === plannedQuantity) {
        showMessage(
            "Plan rozliczony. Można zakończyć zlecenie."
        );
    } else {
        showMessage("Raport dodany pomyślnie.");
    }
});

// 7. Zakończenie zlecenia.
completeButton.addEventListener("click", function () {
    const totalQuantity =
        goodQuantity + scrapQuantity;

    if (
        orderStatus !== "InProgress" ||
        totalQuantity !== plannedQuantity
    ) {
        showMessage(
            "Nie można jeszcze zakończyć zlecenia.",
            true
        );
        return;
    }

    orderStatus = "Completed";

    updateView();
    showMessage("Zlecenie zostało zakończone.");
});

// 8. Reset demonstracyjnego zlecenia.
resetButton.addEventListener("click", function () {
    goodQuantity = 0;
    scrapQuantity = 0;
    orderStatus = "InProgress";

    goodInput.value = "0";
    scrapInput.value = "0";

    updateView();
    showMessage("Dane demonstracyjne zresetowane.");
});

// 9. Wyświetlanie komunikatów o produktach.
function showProductsMessage(text, isError = false) {
    productsMessage.textContent = text;

    productsMessage.classList.toggle(
        "message--error",
        isError
    );
}

// 10. Asynchroniczne pobieranie produktów.
async function loadProducts() {
    loadProductsButton.disabled = true;

    productsList.replaceChildren();

    showProductsMessage("Pobieranie produktów...");

    try {
        // Wywołanie funkcji z modułu productsApi.js.
        const products = await fetchDemoProducts();

        // Wyświetlenie produktów na stronie.
        for (const product of products) {
            const listItem =
                document.createElement("li");

            listItem.textContent =
                `${product.code} - ${product.name}`;

            productsList.appendChild(listItem);
        }

        showProductsMessage(
            `Pobrano ${products.length} produktów.`
        );
    }
    catch (error) {
        console.error(
            "Błąd podczas pobierania produktów:",
            error
        );

        showProductsMessage(
            "Nie udało się pobrać produktów. " +
            "Sprawdź konsolę przeglądarki.",
            true
        );
    }
    finally {
        loadProductsButton.disabled = false;
    }
}

// 11. Obsługa kliknięcia przycisku.
loadProductsButton.addEventListener(
    "click",
    loadProducts
);

// 12. Pierwsze wyświetlenie zlecenia.
updateView();
