
import type {
    OrderOverview,
    ProductionOrderStatus
} from "../types";

interface OrderSummaryProps {
    order: OrderOverview;
}

const statusLabels: Record<ProductionOrderStatus, string> = {
    Draft: "Szkic",
    Released: "Wydane",
    InProgress: "W realizacji",
    Completed: "Zakończone",
    Cancelled: "Anulowane"
};

export default function OrderSummary({
    order
}: OrderSummaryProps) {
    const reportedQuantity =
        order.goodQuantity + order.scrapQuantity;

    const remainingQuantity =
        order.plannedQuantity - reportedQuantity;

    const progressPercent = order.plannedQuantity > 0
        ? Math.round(
            (reportedQuantity / order.plannedQuantity) * 100
        )
        : 0;

    return (
        <section className="card">
            <div className="card-heading">
                <div>
                    <p className="eyebrow">
                        AKTYWNE ZLECENIE
                    </p>

                    <h3>{order.number}</h3>
                </div>

                <span className="status-badge">
                    {statusLabels[order.status]}
                </span>
            </div>

            <p className="product-reference">
                Produkt: {order.productCode}
            </p>

            <div className="statistics">
                <div className="stat">
                    <span>Plan</span>
                    <strong>{order.plannedQuantity}</strong>
                </div>

                <div className="stat">
                    <span>Dobre sztuki</span>
                    <strong>{order.goodQuantity}</strong>
                </div>

                <div className="stat">
                    <span>Braki</span>
                    <strong>{order.scrapQuantity}</strong>
                </div>
            </div>

            <div className="progress-heading">
                <span>Postęp produkcji</span>
                <strong>{progressPercent}%</strong>
            </div>

            <div
                className="progress-track"
                role="progressbar"
                aria-label="Postęp produkcji"
                aria-valuemin={0}
                aria-valuemax={100}
                aria-valuenow={progressPercent}
            >
                <div
                    className="progress-bar"
                    style={{
                        width: `${progressPercent}%`
                    }}
                />
            </div>

            <p className="remaining-text">
                Pozostało {remainingQuantity} sztuk
                do rozliczenia.
            </p>
        </section>
    );
}
