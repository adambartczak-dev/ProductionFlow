
import { useEffect, useState } from "react";
import { Link } from "react-router";

import { ApiHttpError } from "../services/authApi";
import { getMyOrders } from "../services/orderDetailsApi";

import type {
    ProductionOrderResponse
} from "../types";

import "./OrdersPage.css";

import { useOrdersLiveRevision }
    from "../realtime/useOrdersLiveRevision";

interface Props {
    onSessionExpired: () => void;
}

export default function OperatorOrdersPage({
    onSessionExpired
}: Props) {
    const [orders, setOrders] =
        useState<ProductionOrderResponse[]>([]);

    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] =
        useState<string | null>(null);

    const [reload, setReload] = useState(0);

    const liveRevision = useOrdersLiveRevision();

    useEffect(() => {
        const controller = new AbortController();

        async function load() {
            setIsLoading(true);
            setError(null);

            try {
                const data = await getMyOrders(
                    controller.signal
                );

                if (!controller.signal.aborted) {
                    setOrders(data);
                }
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

        void load();

        return () => controller.abort();
    }, [onSessionExpired, reload, liveRevision]);

    return (
        <>
            <div className="page-intro">
                <p className="eyebrow">OPERATOR</p>
                <h2>Moje zlecenia</h2>
                <p>
                    Zlecenia przypisane do Twojego stanowiska
                    (maksymalnie 100).
                </p>
            </div>

            <section className="card orders-card">
                <div className="orders-results-header">
                    <h3>Przypisane zlecenia</h3>

                    <button
                        type="button"
                        disabled={isLoading}
                        onClick={() =>
                            setReload(x => x + 1)
                        }
                    >
                        Odśwież
                    </button>
                </div>

                {isLoading && (
                    <p role="status">
                        Pobieranie zleceń...
                    </p>
                )}

                {error && (
                    <p
                        role="alert"
                        className="orders-error"
                    >
                        {error}
                    </p>
                )}

                {!isLoading &&
                    !error &&
                    orders.length === 0 && (
                        <p>
                            Brak zleceń. Sprawdź, czy administrator
                            przypisał Ci stanowisko.
                        </p>
                    )}

                {!isLoading &&
                    !error &&
                    orders.length > 0 && (
                        <div className="orders-table-scroll">
                            <table className="orders-table">
                                <thead>
                                    <tr>
                                        <th>Numer</th>
                                        <th>Status</th>
                                        <th>Ilość</th>
                                        <th>Termin</th>
                                    </tr>
                                </thead>

                                <tbody>
                                    {orders.map(order => (
                                        <tr key={order.id}>
                                            <td>
                                                <Link
                                                    to={`/orders/${order.id}`}
                                                >
                                                    {order.number}
                                                </Link>
                                            </td>

                                            <td>{order.status}</td>
                                            <td>{order.plannedQuantity}</td>
                                            <td>{order.dueDate}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
            </section>
        </>
    );
}
