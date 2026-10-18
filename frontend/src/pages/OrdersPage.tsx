
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Link } from "react-router";

import { ApiHttpError } from "../services/authApi";
import { getOrders } from "../services/ordersApi";

import type {
    OrderSortBy,
    PagedResult,
    ProductionOrderResponse,
    ProductionOrderStatus
} from "../types";

import "./OrdersPage.css";

import { useOrdersLiveRevision }
    from "../realtime/useOrdersLiveRevision";

interface Props {
    onSessionExpired: () => void;
}

const statusNames: Record<ProductionOrderStatus, string> = {
    Draft: "Szkic",
    Released: "Wydane",
    InProgress: "W realizacji",
    Completed: "Zakończone",
    Cancelled: "Anulowane"
};

export default function OrdersPage({
    onSessionExpired
}: Props) {
    const [searchInput, setSearchInput] = useState("");
    const [search, setSearch] = useState("");

    const [status, setStatus] =
        useState<ProductionOrderStatus | "">("");

    const [sortBy, setSortBy] =
        useState<OrderSortBy>("dueDate");

    const [sortDirection, setSortDirection] =
        useState<"asc" | "desc">("asc");

    const [pageSize, setPageSize] = useState(10);
    const [page, setPage] = useState(1);
    const [reloadKey, setReloadKey] = useState(0);

    const [result, setResult] =
        useState<PagedResult<ProductionOrderResponse> | null>(
            null
        );

    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] =
        useState<string | null>(null);

    const liveRevision = useOrdersLiveRevision();

    useEffect(() => {
        const controller = new AbortController();

        async function loadOrders() {
            setIsLoading(true);
            setError(null);

            try {
                const data = await getOrders(
                    {
                        search,
                        status,
                        sortBy,
                        sortDirection,
                        page,
                        pageSize
                    },
                    controller.signal
                );

                if (controller.signal.aborted) return;

                if (
                    data.totalPages === 0 &&
                    page !== 1
                ) {
                    setPage(1);
                    return;
                }

                if (
                    data.totalPages > 0 &&
                    page > data.totalPages
                ) {
                    setPage(data.totalPages);
                    return;
                }

                setResult(data);
            }
            catch (caught) {
                if (controller.signal.aborted) return;

                if (
                    caught instanceof ApiHttpError &&
                    caught.status === 401
                ) {
                    onSessionExpired();
                    return;
                }

                setResult(null);

                setError(
                    caught instanceof Error
                        ? caught.message
                        : "Błąd pobierania zleceń."
                );
            }
            finally {
                if (!controller.signal.aborted) {
                    setIsLoading(false);
                }
            }
        }

        void loadOrders();

        return () => controller.abort();
    }, [
        search,
        status,
        sortBy,
        sortDirection,
        page,
        pageSize,
        reloadKey,
        onSessionExpired,
        liveRevision
    ]);

    function handleSearch(
        event: FormEvent<HTMLFormElement>
    ) {
        event.preventDefault();

        setSearch(searchInput.trim());
        setPage(1);
    }

    function clearFilters() {
        setSearchInput("");
        setSearch("");
        setStatus("");
        setSortBy("dueDate");
        setSortDirection("asc");
        setPageSize(10);
        setPage(1);

        setReloadKey(value => value + 1);
    }

    const totalPages = result?.totalPages ?? 0;

    const displayedPage = totalPages === 0
        ? 0
        : result?.page ?? page;

    return (
        <>
            <div className="page-intro">
                <p className="eyebrow">PLANOWANIE</p>

                <h2>Zlecenia produkcyjne</h2>

                <p>
                    Wyszukiwanie, filtrowanie i stronicowanie
                    danych z PostgreSQL.
                </p>
            </div>

            <section className="card orders-card">
                <h3>Wyszukiwanie i filtry</h3>

                <form
                    onSubmit={handleSearch}
                    className="orders-search-form"
                >
                    <label htmlFor="orders-search">
                        Numer zlecenia
                    </label>

                    <div className="orders-search-row">
                        <input
                            id="orders-search"
                            type="search"
                            value={searchInput}
                            onChange={event =>
                                setSearchInput(event.target.value)
                            }
                            placeholder="np. PF-0001"
                        />

                        <button type="submit">
                            Szukaj
                        </button>
                    </div>
                </form>

                <div className="orders-filter-grid">
                    <label>
                        Status

                        <select
                            value={status}
                            onChange={event => {
                                setStatus(
                                    event.target.value as
                                    ProductionOrderStatus | ""
                                );
                                setPage(1);
                            }}
                        >
                            <option value="">Wszystkie</option>
                            <option value="Draft">Szkic</option>
                            <option value="Released">Wydane</option>
                            <option value="InProgress">W realizacji</option>
                            <option value="Completed">Zakończone</option>
                            <option value="Cancelled">Anulowane</option>
                        </select>
                    </label>

                    <label>
                        Sortuj według

                        <select
                            value={sortBy}
                            onChange={event => {
                                setSortBy(
                                    event.target.value as OrderSortBy
                                );
                                setPage(1);
                            }}
                        >
                            <option value="dueDate">Terminu</option>
                            <option value="number">Numeru</option>
                            <option value="plannedQuantity">
                                Planowanej ilości
                            </option>
                            <option value="status">Statusu</option>
                        </select>
                    </label>

                    <label>
                        Kierunek

                        <select
                            value={sortDirection}
                            onChange={event => {
                                setSortDirection(
                                    event.target.value as "asc" | "desc"
                                );
                                setPage(1);
                            }}
                        >
                            <option value="asc">Rosnąco</option>
                            <option value="desc">Malejąco</option>
                        </select>
                    </label>

                    <label>
                        Na stronie

                        <select
                            value={pageSize}
                            onChange={event => {
                                setPageSize(
                                    Number(event.target.value)
                                );
                                setPage(1);
                            }}
                        >
                            <option value={5}>5</option>
                            <option value={10}>10</option>
                            <option value={20}>20</option>
                        </select>
                    </label>
                </div>

                <div className="orders-actions">
                    <button
                        type="button"
                        disabled={isLoading}
                        onClick={() =>
                            setReloadKey(value => value + 1)
                        }
                    >
                        Odśwież listę
                    </button>

                    <button
                        type="button"
                        className="orders-secondary"
                        onClick={clearFilters}
                    >
                        Wyczyść filtry
                    </button>
                </div>
            </section>

            <section className="card orders-card">
                <div className="orders-results-header">
                    <h3>Lista zleceń</h3>

                    {!isLoading && result && (
                        <span>
                            Wyników: {result.totalCount}
                        </span>
                    )}
                </div>

                {isLoading && (
                    <p role="status">
                        Ładowanie zleceń...
                    </p>
                )}

                {!isLoading && error && (
                    <p
                        role="alert"
                        className="orders-error"
                    >
                        {error}
                    </p>
                )}

                {!isLoading &&
                    !error &&
                    result?.items.length === 0 && (
                        <p role="status">
                            Brak zleceń spełniających kryteria.
                        </p>
                    )}

                {!isLoading &&
                    result &&
                    result.items.length > 0 && (
                        <div className="orders-table-scroll">
                            <table className="orders-table">
                                <thead>
                                    <tr>
                                        <th>Numer</th>
                                        <th>Status</th>
                                        <th>Planowana ilość</th>
                                        <th>Termin</th>
                                    </tr>
                                </thead>

                                <tbody>
                                    {result.items.map(order => (
                                        <tr key={order.id}>
                                            <td>
                                                <Link
                                                    to={`/orders/${order.id}`}
                                                >
                                                    {order.number}
                                                </Link>
                                            </td>

                                            <td>
                                                {statusNames[order.status]}
                                            </td>

                                            <td>
                                                {order.plannedQuantity}
                                            </td>

                                            <td>
                                                {order.dueDate
                                                    .split("-")
                                                    .reverse()
                                                    .join(".")}
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}

                {!isLoading &&
                    !error &&
                    result && (
                        <div className="orders-pagination">
                            <button
                                disabled={page <= 1}
                                type="button"
                                onClick={() =>
                                    setPage(current =>
                                        Math.max(1, current - 1)
                                    )
                                }
                            >
                                Poprzednia
                            </button>

                            <span>
                                Strona {displayedPage}
                                {" "}z {totalPages}
                            </span>

                            <button
                                disabled={
                                    totalPages === 0 ||
                                    page >= totalPages
                                }
                                type="button"
                                onClick={() =>
                                    setPage(current => current + 1)
                                }
                            >
                                Następna
                            </button>
                        </div>
                    )}
            </section>

            <p className="footer-note">
                Kliknij numer zlecenia, aby wyświetlić szczegóły.
            </p>
        </>
    );
}
