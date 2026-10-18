
"use strict";

// Pobranie przykładowych produktów przez HTTP.
export async function fetchDemoProducts() {
    const response = await fetch(
        "./data/products.json",
        { cache: "no-store" }
    );

    if (!response.ok) {
        throw new Error(
            `Nie udało się pobrać produktów. HTTP ${response.status}`
        );
    }

    const products = await response.json();

    if (!Array.isArray(products)) {
        throw new Error(
            "Serwer nie zwrócił listy produktów."
        );
    }

    return products;
}
