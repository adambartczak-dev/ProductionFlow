
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Link } from "react-router";

import { ApiHttpError } from "../services/authApi";
import { createOrder } from "../services/ordersApi";
import { getProducts } from "../services/productsApi";
import { getWorkCenters } from "../services/workCentersApi";

import type {
    CreateProductionOrderRequest,
    ProductResponse,
    ProductionOrderResponse,
    WorkCenterResponse
} from "../types";

import "./NewOrderPage.css";

interface NewOrderPageProps {
    onSessionExpired: () => void;
}

interface NewOrderForm {
    number: string;
    productId: string;
    workCenterId: string;
    plannedQuantity: string;
    dueDate: string;
}

const emptyForm: NewOrderForm = {
    number: "",
    productId: "",
    workCenterId: "",
    plannedQuantity: "",
    dueDate: ""
};

// Sprawdzamy nie tylko format, ale też faktyczną datę.
function isValidDate(value: string): boolean {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
        return false;
    }

    const date = new Date(`${value}T00:00:00Z`);

    return !Number.isNaN(date.getTime()) &&
        date.toISOString().startsWith(value);
}

export default function NewOrderPage({
    onSessionExpired
}: NewOrderPageProps) {
    const [form, setForm] = useState<NewOrderForm>(
        { ...emptyForm }
    );

    const [products, setProducts] = useState<ProductResponse[]>([]);
    const [workCenters, setWorkCenters] =
        useState<WorkCenterResponse[]>([]);

    const [isLoading, setIsLoading] = useState(true);
    const [loadError, setLoadError] = useState<string | null>(null);
    const [reloadKey, setReloadKey] = useState(0);

    const [isSaving, setIsSaving] = useState(false);
    const [submitError, setSubmitError] =
        useState<string | null>(null);
    const [createdOrder, setCreatedOrder] =
        useState<ProductionOrderResponse | null>(null);

    // Produkty i stanowiska pobieramy równolegle z API.
    useEffect(() => {
        const controller = new AbortController();

        async function loadCatalogs() {
            setIsLoading(true);
            setLoadError(null);

            try {
                const [loadedProducts, loadedWorkCenters] =
                    await Promise.all([
                        getProducts(controller.signal),
                        getWorkCenters(controller.signal)
                    ]);

                if (controller.signal.aborted) {
                    return;
                }

                setProducts(loadedProducts);
                setWorkCenters(loadedWorkCenters);
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

                setLoadError(
                    error instanceof Error
                        ? error.message
                        : "Nie udało się pobrać katalogów."
                );
            }
            finally {
                if (!controller.signal.aborted) {
                    setIsLoading(false);
                }
            }
        }

        void loadCatalogs();

        return () => controller.abort();
    }, [reloadKey, onSessionExpired]);

    // Wartości inputów są przechowywane w stanie React.
    function updateField(
        field: keyof NewOrderForm,
        value: string
    ) {
        setForm(previous => ({
            ...previous,
            [field]: value
        }));

        setSubmitError(null);
        setCreatedOrder(null);
    }

    async function handleSubmit(
        event: FormEvent<HTMLFormElement>
    ) {
        event.preventDefault();

        if (isSaving || isLoading || loadError) {
            return;
        }

        const number = form.number.trim();
        const quantity = Number(form.plannedQuantity);

        if (number.length < 1 || number.length > 50) {
            setSubmitError(
                "Numer zlecenia musi zawierać od 1 do 50 znaków."
            );
            return;
        }

        if (!form.productId || !form.workCenterId) {
            setSubmitError("Wybierz produkt i stanowisko.");
            return;
        }

        if (
            !form.plannedQuantity.trim() ||
            !Number.isInteger(quantity) ||
            quantity < 1 ||
            quantity > 2147483647
        ) {
            setSubmitError(
                "Planowana ilość musi być liczbą całkowitą " +
                "od 1 do 2147483647."
            );
            return;
        }

        if (!isValidDate(form.dueDate)) {
            setSubmitError("Wybierz poprawny termin realizacji.");
            return;
        }

        const request: CreateProductionOrderRequest = {
            number,
            productId: form.productId,
            workCenterId: form.workCenterId,
            plannedQuantity: quantity,
            dueDate: form.dueDate
        };

        setSubmitError(null);
        setCreatedOrder(null);
        setIsSaving(true);

        try {
            const result = await createOrder(request);

            setCreatedOrder(result);
            setForm({ ...emptyForm });
        }
        catch (error) {
            if (
                error instanceof ApiHttpError &&
                error.status === 401
            ) {
                onSessionExpired();
                return;
            }

            setSubmitError(
                error instanceof Error
                    ? error.message
                    : "Nieznany błąd tworzenia zlecenia."
            );
        }
        finally {
            setIsSaving(false);
        }
    }

    function handleReset() {
        setForm({ ...emptyForm });
        setSubmitError(null);
        setCreatedOrder(null);
    }

    const catalogsReady = !isLoading &&
        !loadError &&
        products.length > 0 &&
        workCenters.length > 0;

    return (
        <>
            <div className="page-intro">
                <p className="eyebrow">PLANOWANIE</p>
                <h2>Nowe zlecenie</h2>
                <p>
                    Utwórz zlecenie w bazie danych ProductionFlow.
                </p>
            </div>

            <section className="card draft-card">
                <h3>Dane zlecenia</h3>
                <p className="draft-description">
                    Produkty i stanowiska są pobierane z REST API.
                    Nowe zlecenie otrzyma status Szkic.
                </p>

                {isLoading && (
                    <p role="status">Pobieranie katalogów...</p>
                )}

                {loadError && (
                    <div role="alert" className="draft-error">
                        <p>{loadError}</p>
                        <button
                            className="draft-button draft-button--secondary"
                            type="button"
                            onClick={() => setReloadKey(key => key + 1)}
                        >
                            Spróbuj ponownie
                        </button>
                    </div>
                )}

                {!isLoading && !loadError && !catalogsReady && (
                    <p role="alert" className="draft-error">
                        Brak produktów lub stanowisk w bazie.
                        Dodaj je do katalogów przed utworzeniem zlecenia.
                    </p>
                )}

                {catalogsReady && (
                    <form onSubmit={event => void handleSubmit(event)} noValidate>
                        <div className="draft-grid">
                            <div className="draft-field">
                                <label htmlFor="order-number">Numer zlecenia</label>
                                <input
                                    id="order-number"
                                    type="text"
                                    maxLength={50}
                                    placeholder="np. PF-0002"
                                    value={form.number}
                                    onChange={event => updateField(
                                        "number", event.target.value
                                    )}
                                    disabled={isSaving}
                                    required
                                />
                            </div>

                            <div className="draft-field">
                                <label htmlFor="order-product">Produkt</label>
                                <select
                                    id="order-product"
                                    value={form.productId}
                                    onChange={event => updateField(
                                        "productId", event.target.value
                                    )}
                                    disabled={isSaving}
                                    required
                                >
                                    <option value="">Wybierz produkt</option>
                                    {products.map(product => (
                                        <option key={product.id} value={product.id}>
                                            {product.code} — {product.name}
                                        </option>
                                    ))}
                                </select>
                            </div>

                            <div className="draft-field">
                                <label htmlFor="order-workcenter">
                                    Stanowisko produkcyjne
                                </label>
                                <select
                                    id="order-workcenter"
                                    value={form.workCenterId}
                                    onChange={event => updateField(
                                        "workCenterId", event.target.value
                                    )}
                                    disabled={isSaving}
                                    required
                                >
                                    <option value="">Wybierz stanowisko</option>
                                    {workCenters.map(workCenter => (
                                        <option key={workCenter.id} value={workCenter.id}>
                                            {workCenter.code} — {workCenter.name}
                                        </option>
                                    ))}
                                </select>
                            </div>

                            <div className="draft-field">
                                <label htmlFor="order-quantity">
                                    Planowana ilość
                                </label>
                                <input
                                    id="order-quantity"
                                    type="number"
                                    min="1"
                                    max="2147483647"
                                    step="1"
                                    placeholder="np. 200"
                                    value={form.plannedQuantity}
                                    onChange={event => updateField(
                                        "plannedQuantity", event.target.value
                                    )}
                                    disabled={isSaving}
                                    required
                                />
                            </div>

                            <div className="draft-field">
                                <label htmlFor="order-date">Termin realizacji</label>
                                <input
                                    id="order-date"
                                    type="date"
                                    value={form.dueDate}
                                    onChange={event => updateField(
                                        "dueDate", event.target.value
                                    )}
                                    disabled={isSaving}
                                    required
                                />
                            </div>
                        </div>

                        {submitError && (
                            <p className="draft-error" role="alert">
                                {submitError}
                            </p>
                        )}

                        <div className="draft-actions">
                            <button
                                type="submit"
                                className="draft-button"
                                disabled={isSaving}
                            >
                                {isSaving ? "Zapisywanie..." : "Utwórz zlecenie"}
                            </button>

                            <button
                                type="button"
                                className="draft-button draft-button--secondary"
                                onClick={handleReset}
                                disabled={isSaving}
                            >
                                Wyczyść
                            </button>
                        </div>
                    </form>
                )}

                {createdOrder && (
                    <section className="draft-preview" role="status">
                        <h4>Zlecenie zostało utworzone</h4>
                        <p>
                            Numer: <strong>{createdOrder.number}</strong>
                        </p>
                        <p>Status: Szkic (Draft).</p>
                        <p>
                            <Link to="/orders">
                                Przejdź do listy zleceń
                            </Link>
                        </p>
                    </section>
                )}
            </section>
        </>
    );
}
