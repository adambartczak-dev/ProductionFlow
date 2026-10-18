
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Link, useParams } from "react-router";

import { ApiHttpError } from "../services/authApi";
import { getProducts } from "../services/productsApi";
import { getWorkCenters } from "../services/workCentersApi";

import {
    addExecutionReport,
    changeOrderStatus,
    getHistory,
    getOrder,
    getProgress,
    getReports
} from "../services/orderDetailsApi";

import {
    canPerformOrderAction,
    canPlanOrders
} from "../utils/orderPermissions";

import type {
    AuthUser,
    ExecutionReportResponse,
    OrderHistoryResponse,
    ProductResponse,
    ProductionOrderResponse,
    ProductionOrderStatus,
    ProductionProgressResponse,
    WorkCenterResponse
} from "../types";

import "./OrderDetailsPage.css";

import { useOrdersLiveRevision }
    from "../realtime/useOrdersLiveRevision";

interface Props {
    user: AuthUser;
    onSessionExpired: () => void;
}

type Feedback = {
    kind: "conflict" | "denied" | "error";
    message: string;
};

// Wszystkie dane pobierane dla jednego zlecenia.
interface OrderDetailsData {
    order: ProductionOrderResponse;
    progress: ProductionProgressResponse;
    history: OrderHistoryResponse[];
    reports: ExecutionReportResponse[];
    products: ProductResponse[];
    workCenters: WorkCenterResponse[];
}

// Wynik pobierania danych powiązany z konkretnym żądaniem.
interface LoadState {
    key: string;
    data: OrderDetailsData | null;
    error: string | null;
}

const statusNames: Record<ProductionOrderStatus, string> = {
    Draft: "Szkic",
    Released: "Wydane",
    InProgress: "W realizacji",
    Completed: "Zakończone",
    Cancelled: "Anulowane"
};

function formatDate(date: string): string {
    return date.split("-").reverse().join(".");
}

function formatTime(value: string): string {
    return new Date(value).toLocaleString("pl-PL");
}

export default function OrderDetailsPage({
    user,
    onSessionExpired
}: Props) {
    const { id } = useParams<{ id: string }>();

    // Numer aktualnego żądania odświeżenia.
    const [reloadKey, setReloadKey] = useState(0);

    // Stan pobierania danych.
    const [loadState, setLoadState] = useState<LoadState>({
        key: "",
        data: null,
        error: null
    });

    // Stan operacji użytkownika.
    const [isSaving, setIsSaving] = useState(false);

    const [feedback, setFeedback] =
        useState<Feedback | null>(null);

    const [success, setSuccess] =
        useState<string | null>(null);

    const [needsRefresh, setNeedsRefresh] =
        useState(false);

    // Pola formularza raportowania.
    const [goodQuantity, setGoodQuantity] = useState("0");
    const [scrapQuantity, setScrapQuantity] = useState("0");

    // Każde odświeżenie otrzymuje osobny klucz.
    const liveRevision = useOrdersLiveRevision(id);

    const requestKey =
        `${id ?? "missing"}:${reloadKey}:${liveRevision}`;

    // Obliczamy ładowanie bez dodatkowego setState().
    const isLoading =
        Boolean(id) && loadState.key !== requestKey;

    // Błąd brakującego ID wynika bezpośrednio z adresu URL.
    const loadError = !id
        ? "Nieprawidłowy identyfikator zlecenia."
        : isLoading
            ? null
            : loadState.error;

    // Nie pokazujemy starych danych podczas odświeżania.
    const details = isLoading
        ? null
        : loadState.data;

    const order = details?.order ?? null;
    const progress = details?.progress ?? null;
    const history = details?.history ?? [];
    const reports = details?.reports ?? [];
    const products = details?.products ?? [];
    const workCenters = details?.workCenters ?? [];

    const backTo = canPlanOrders(user)
        ? "/orders"
        : "/operator/orders";

    // Pobieranie danych z API.
    useEffect(() => {
        if (!id) {
            return;
        }

        const orderId = id;
        const controller = new AbortController();

        async function load() {
            try {
                const [
                    currentOrder,
                    currentProgress,
                    currentHistory,
                    currentReports,
                    currentProducts,
                    currentWorkCenters
                ] = await Promise.all([
                    getOrder(orderId, controller.signal),
                    getProgress(orderId, controller.signal),
                    getHistory(orderId, controller.signal),
                    getReports(orderId, controller.signal),
                    getProducts(controller.signal),
                    getWorkCenters(controller.signal)
                ]);

                if (controller.signal.aborted) {
                    return;
                }

                // Aktualizacja dopiero po zakończeniu
                // asynchronicznego pobierania danych.
                setLoadState({
                    key: requestKey,
                    data: {
                        order: currentOrder,
                        progress: currentProgress,
                        history: currentHistory,
                        reports: currentReports,
                        products: currentProducts,
                        workCenters: currentWorkCenters
                    },
                    error: null
                });

                setNeedsRefresh(false);
                setFeedback(null);
            }
            catch (error) {
                if (controller.signal.aborted) {
                    return;
                }

                if (
                    error instanceof ApiHttpError &&
                    error.status === 401
                ) {
                    onSessionExpired();
                    return;
                }

                setLoadState({
                    key: requestKey,
                    data: null,
                    error: error instanceof Error
                        ? error.message
                        : "Nie można pobrać szczegółów zlecenia."
                });
            }
        }

        void load();

        return () => controller.abort();
    }, [id, requestKey, onSessionExpired]);

    // Ręczne odświeżenie danych.
    function refreshDetails() {
        if (isSaving || isLoading || !id) {
            return;
        }

        setSuccess(null);
        setReloadKey(value => value + 1);
    }

    // Obsługa błędów operacji zapisu.
    function processActionError(error: unknown) {
        if (
            error instanceof ApiHttpError &&
            error.status === 401
        ) {
            onSessionExpired();
            return;
        }

        if (
            error instanceof ApiHttpError &&
            error.status === 409
        ) {
            setNeedsRefresh(true);

            setFeedback({
                kind: "conflict",
                message: error.message
            });

            return;
        }

        if (
            error instanceof ApiHttpError &&
            error.status === 403
        ) {
            setFeedback({
                kind: "denied",
                message:
                    "Serwer odrzucił tę operację: brak uprawnień. " +
                    "Możliwe, że zmieniło się przypisanie operatora."
            });

            return;
        }

        // Nie mamy pewności, czy operacja została wykonana.
        setNeedsRefresh(true);

        setFeedback({
            kind: "error",
            message: error instanceof Error
                ? error.message
                : "Nie można potwierdzić wyniku operacji."
        });
    }

    // Wydanie, rozpoczęcie lub zakończenie zlecenia.
    async function runAction(
        operation: "release" | "start" | "complete"
    ) {
        if (
            !id ||
            !order ||
            !progress ||
            isSaving ||
            isLoading ||
            needsRefresh
        ) {
            return;
        }

        if (!canPerformOrderAction(
            user,
            order.status,
            progress.remainingQuantity,
            operation
        )) {
            return;
        }

        setIsSaving(true);
        setFeedback(null);
        setSuccess(null);

        try {
            await changeOrderStatus(id, operation);

            setSuccess(
                "Zmiana statusu została zapisana."
            );

            setReloadKey(value => value + 1);
        }
        catch (error) {
            processActionError(error);
        }
        finally {
            setIsSaving(false);
        }
    }

    // Dodawanie raportu wykonania.
    async function submitReport(
        event: FormEvent<HTMLFormElement>
    ) {
        event.preventDefault();

        if (
            !id ||
            !order ||
            !progress ||
            isSaving ||
            isLoading ||
            needsRefresh
        ) {
            return;
        }

        if (!canPerformOrderAction(
            user,
            order.status,
            progress.remainingQuantity,
            "report"
        )) {
            return;
        }

        const good = Number(goodQuantity);
        const scrap = Number(scrapQuantity);
        const total = good + scrap;

        if (
            !goodQuantity.trim() ||
            !scrapQuantity.trim() ||
            !Number.isSafeInteger(good) ||
            !Number.isSafeInteger(scrap) ||
            good < 0 ||
            scrap < 0 ||
            total < 1 ||
            total > progress.remainingQuantity
        ) {
            setFeedback({
                kind: "error",
                message:
                    "Podaj nieujemne liczby całkowite. " +
                    "Ich suma musi mieścić się w zakresie " +
                    `1–${progress.remainingQuantity}.`
            });

            return;
        }

        setIsSaving(true);
        setFeedback(null);
        setSuccess(null);

        try {
            await addExecutionReport(id, {
                operatorId: user.id,
                goodQuantity: good,
                scrapQuantity: scrap
            });

            setGoodQuantity("0");
            setScrapQuantity("0");

            setSuccess(
                "Raport został zapisany w bazie."
            );

            setReloadKey(value => value + 1);
        }
        catch (error) {
            // Zachowujemy wpisane ilości.
            processActionError(error);
        }
        finally {
            setIsSaving(false);
        }
    }

    const blockActions =
        isSaving ||
        isLoading ||
        needsRefresh ||
        !!loadError;

    return (
        <>
            <div className="page-intro">
                <p className="eyebrow">
                    REALIZACJA PRODUKCJI
                </p>

                <h2>Szczegóły zlecenia</h2>

                <p>
                    <Link to={backTo}>
                        ← Wróć do listy zleceń
                    </Link>
                </p>
            </div>

            {isLoading && (
                <p role="status">
                    Pobieranie bieżącego stanu zlecenia...
                </p>
            )}

            {loadError && (
                <section
                    className="card details-card"
                    role="alert"
                >
                    <p className="details-error">
                        {loadError}
                    </p>

                    {id && (
                        <button
                            type="button"
                            onClick={refreshDetails}
                            disabled={isLoading || isSaving}
                        >
                            Spróbuj ponownie
                        </button>
                    )}
                </section>
            )}

            {feedback && (
                <section
                    className="card details-card"
                    role="alert"
                >
                    <h3>
                        {feedback.kind === "conflict"
                            ? "Konflikt danych (HTTP 409)"
                            : feedback.kind === "denied"
                                ? "Brak uprawnień (HTTP 403)"
                                : "Nie udało się potwierdzić operacji"}
                    </h3>

                    <p className="details-error">
                        {feedback.message}
                    </p>

                    {needsRefresh && (
                        <>
                            <p>
                                Wyświetlone informacje mogą być
                                nieaktualne. Najpierw pobierz
                                aktualny stan z serwera.
                                Nie ponawiamy zapisu automatycznie.
                            </p>

                            <button
                                type="button"
                                onClick={refreshDetails}
                                disabled={isLoading || isSaving}
                            >
                                Pobierz aktualny stan
                            </button>
                        </>
                    )}
                </section>
            )}

            {success && (
                <p
                    role="status"
                    className="details-success"
                >
                    {success}
                </p>
            )}

            {!isLoading && !loadError && order && progress && (
                <>
                    <section className="card details-card">
                        <div className="details-heading">
                            <h3>{order.number}</h3>

                            <strong>
                                {statusNames[order.status]}
                            </strong>
                        </div>

                        <dl className="details-grid">
                            <div>
                                <dt>Produkt</dt>
                                <dd>
                                    {products.find(
                                        p => p.id === order.productId
                                    )?.name ?? order.productId}
                                </dd>
                            </div>

                            <div>
                                <dt>Stanowisko</dt>
                                <dd>
                                    {workCenters.find(
                                        c => c.id === order.workCenterId
                                    )?.name ?? order.workCenterId}
                                </dd>
                            </div>

                            <div>
                                <dt>Termin</dt>
                                <dd>{formatDate(order.dueDate)}</dd>
                            </div>

                            <div>
                                <dt>Planowana ilość</dt>
                                <dd>{order.plannedQuantity}</dd>
                            </div>
                        </dl>

                        <div className="details-actions">
                            {canPerformOrderAction(
                                user,
                                order.status,
                                progress.remainingQuantity,
                                "release"
                            ) && (
                                    <button
                                        type="button"
                                        disabled={blockActions}
                                        onClick={() =>
                                            void runAction("release")
                                        }
                                    >
                                        Wydaj do produkcji
                                    </button>
                                )}

                            {canPerformOrderAction(
                                user,
                                order.status,
                                progress.remainingQuantity,
                                "start"
                            ) && (
                                    <button
                                        type="button"
                                        disabled={blockActions}
                                        onClick={() =>
                                            void runAction("start")
                                        }
                                    >
                                        Rozpocznij realizację
                                    </button>
                                )}

                            {canPerformOrderAction(
                                user,
                                order.status,
                                progress.remainingQuantity,
                                "complete"
                            ) && (
                                    <button
                                        type="button"
                                        disabled={blockActions}
                                        onClick={() =>
                                            void runAction("complete")
                                        }
                                    >
                                        Zakończ zlecenie
                                    </button>
                                )}

                            <button
                                type="button"
                                className="details-secondary"
                                disabled={isSaving || isLoading}
                                onClick={refreshDetails}
                            >
                                Odśwież
                            </button>
                        </div>

                        <p>
                            Widoczne akcje zależą od roli i statusu.
                            Serwer dodatkowo sprawdza przypisane
                            stanowisko operatora.
                        </p>
                    </section>

                    <section className="card details-card">
                        <h3>Postęp produkcji</h3>

                        <div
                            className="details-progress"
                            role="progressbar"
                            aria-label="Postęp realizacji"
                            aria-valuemin={0}
                            aria-valuemax={100}
                            aria-valuenow={Math.min(
                                100,
                                Math.max(
                                    0,
                                    progress.progressPercent
                                )
                            )}
                        >
                            <div
                                style={{
                                    width: `${Math.min(
                                        100,
                                        Math.max(
                                            0,
                                            progress.progressPercent
                                        )
                                    )
                                        }%`
                                }}
                            />
                        </div>

                        <p>
                            <strong>
                                {progress.progressPercent}%
                            </strong>
                            {" "}— wykonano {progress.totalQuantity}
                            {" "}z {progress.plannedQuantity} sztuk
                        </p>

                        <p>
                            Dobre: {progress.goodQuantity}
                            {" | "}Braki: {progress.scrapQuantity}
                            {" | "}Pozostało: {progress.remainingQuantity}
                        </p>
                    </section>

                    {canPerformOrderAction(
                        user,
                        order.status,
                        progress.remainingQuantity,
                        "report"
                    ) && (
                            <section className="card details-card">
                                <h3>Nowy raport wykonania</h3>

                                <p>
                                    Raport zostanie przypisany
                                    do zalogowanego użytkownika.
                                </p>

                                <form
                                    onSubmit={event =>
                                        void submitReport(event)
                                    }
                                >
                                    <div className="details-report-grid">
                                        <label>
                                            Dobre sztuki

                                            <input
                                                type="number"
                                                min="0"
                                                max="2147483647"
                                                step="1"
                                                value={goodQuantity}
                                                disabled={blockActions}
                                                onChange={event =>
                                                    setGoodQuantity(
                                                        event.target.value
                                                    )
                                                }
                                                required
                                            />
                                        </label>

                                        <label>
                                            Braki

                                            <input
                                                type="number"
                                                min="0"
                                                max="2147483647"
                                                step="1"
                                                value={scrapQuantity}
                                                disabled={blockActions}
                                                onChange={event =>
                                                    setScrapQuantity(
                                                        event.target.value
                                                    )
                                                }
                                                required
                                            />
                                        </label>
                                    </div>

                                    <button
                                        type="submit"
                                        disabled={blockActions}
                                    >
                                        {isSaving
                                            ? "Zapisywanie..."
                                            : "Zapisz raport"}
                                    </button>
                                </form>
                            </section>
                        )}

                    <section className="card details-card">
                        <h3>
                            Raporty wykonania ({reports.length})
                        </h3>

                        {reports.length === 0
                            ? <p>Brak raportów.</p>
                            : (
                                <div className="details-table-wrap">
                                    <table className="details-table">
                                        <thead>
                                            <tr>
                                                <th>Data</th>
                                                <th>Operator (ID)</th>
                                                <th>Dobre</th>
                                                <th>Braki</th>
                                            </tr>
                                        </thead>

                                        <tbody>
                                            {reports.map(report => (
                                                <tr key={report.id}>
                                                    <td>
                                                        {formatTime(
                                                            report.createdAtUtc
                                                        )}
                                                    </td>
                                                    <td>
                                                        {report.operatorId}
                                                    </td>
                                                    <td>
                                                        {report.goodQuantity}
                                                    </td>
                                                    <td>
                                                        {report.scrapQuantity}
                                                    </td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                </div>
                            )}
                    </section>

                    <section className="card details-card">
                        <h3>
                            Historia zmian ({history.length})
                        </h3>

                        {history.length === 0
                            ? <p>Brak zapisanych zdarzeń.</p>
                            : (
                                <ol className="details-history">
                                    {history.map(entry => (
                                        <li key={entry.id}>
                                            <strong>
                                                {formatTime(
                                                    entry.occurredAtUtc
                                                )}
                                            </strong>

                                            <span>
                                                {entry.details}
                                            </span>

                                            {entry.actor && (
                                                <small>
                                                    Autor: {entry.actor}
                                                </small>
                                            )}
                                        </li>
                                    ))}
                                </ol>
                            )}
                    </section>
                </>
            )}
        </>
    );
}
