
import { useEffect, useState } from "react";

import ProductList from "../components/ProductList";

import { ApiHttpError } from "../services/authApi";
import { getProducts } from "../services/productsApi";

import type { ProductResponse } from "../types";

interface ProductsPageProps {
    onSessionExpired: () => void;
}

export default function ProductsPage({
    onSessionExpired
}: ProductsPageProps) {
    const [products, setProducts] =
        useState<ProductResponse[]>([]);

    const [isLoading, setIsLoading] =
        useState(true);

    const [error, setError] =
        useState<string | null>(null);

    const [reloadKey, setReloadKey] =
        useState(0);

    // Pobranie produktów.
    useEffect(() => {
        const controller = new AbortController();

        async function loadProducts() {
            setIsLoading(true);
            setError(null);

            try {
                const data = await getProducts(
                    controller.signal
                );

                if (!controller.signal.aborted) {
                    setProducts(data);
                }
            }
            catch (caughtError) {
                if (controller.signal.aborted) {
                    return;
                }

                // Wygasła sesja.
                if (
                    caughtError instanceof ApiHttpError &&
                    caughtError.status === 401
                ) {
                    onSessionExpired();
                    return;
                }

                // Inny problem, np. 403 lub 500.
                setProducts([]);

                setError(
                    caughtError instanceof Error
                        ? caughtError.message
                        : "Nieznany błąd pobierania produktów."
                );
            }
            finally {
                if (!controller.signal.aborted) {
                    setIsLoading(false);
                }
            }
        }

        void loadProducts();

        return () => controller.abort();
    }, [reloadKey, onSessionExpired]);

    function handleReload() {
        setReloadKey(
            (previous) => previous + 1
        );
    }

    return (
        <>
            <div className="page-intro">
                <p className="eyebrow">
                    KATALOG
                </p>

                <h2>Produkty</h2>

                <p>
                    Katalog produktów pobieranych
                    z bazy PostgreSQL.
                </p>
            </div>

            <div className="products-page">
                <ProductList
                    products={products}
                    isLoading={isLoading}
                    error={error}
                    onReload={handleReload}
                />
            </div>

            <p className="footer-note">
                Produkty z GET /api/products.
                Brak sesji powoduje powrót do logowania.
            </p>
        </>
    );
}
