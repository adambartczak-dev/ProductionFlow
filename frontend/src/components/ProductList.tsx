
import type { ProductResponse } from "../types";
import "./ProductList.css";

interface ProductListProps {
    products: ProductResponse[];
    isLoading: boolean;
    error: string | null;
    onReload: () => void;
}

export default function ProductList({
    products,
    isLoading,
    error,
    onReload
}: ProductListProps) {
    return (
        <section className="card">
            <div className="catalog-heading">
                <h3>Katalog produktów</h3>

                <span className="product-count">
                    {isLoading || error
                        ? "—"
                        : `${products.length} produktów`}
                </span>
            </div>

            {isLoading && (
                <p
                    role="status"
                    className="catalog-message"
                >
                    Ładowanie produktów...
                </p>
            )}

            {!isLoading && error && (
                <p
                    role="alert"
                    className="catalog-message catalog-error"
                >
                    {error}
                </p>
            )}

            {!isLoading &&
                !error &&
                products.length === 0 && (
                    <p className="catalog-message">
                        Brak produktów w bazie danych.
                    </p>
                )}

            {!isLoading &&
                !error &&
                products.length > 0 && (
                    <ul className="product-list">
                        {products.map((product) => (
                            <li
                                key={product.id}
                                className="product-item"
                            >
                                <div className="product-icon">
                                    P
                                </div>

                                <div className="product-details">
                                    <strong>
                                        {product.code}
                                    </strong>

                                    <span>
                                        {product.name}
                                    </span>
                                </div>
                            </li>
                        ))}
                    </ul>
                )}

            <button
                type="button"
                className="catalog-reload-button"
                onClick={onReload}
                disabled={isLoading}
            >
                {isLoading
                    ? "Pobieranie..."
                    : "Odśwież produkty"}
            </button>
        </section>
    );
}
