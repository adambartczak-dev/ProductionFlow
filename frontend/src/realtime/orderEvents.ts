
export interface ProductionOrderChanged {
    orderId: string;
    workCenterId: string;
    changeType: string;
    status: string;
    occurredAtUtc: string;
}

// Zdarzenie zmiany pojedynczego zlecenia
// albo potrzeby pełnej synchronizacji.
export type ProductionRealtimeEvent =
    | {
        type: "changed";
        order: ProductionOrderChanged;
    }
    | {
        type: "resync";
    };

type Listener = (
    event: ProductionRealtimeEvent
) => void;

// Subskrybenci znajdujący się aktualnie na stronach React.
const listeners = new Set<Listener>();

// Dodanie subskrybenta i funkcja do jego usunięcia.
export function subscribeToOrderEvents(
    listener: Listener
): () => void {
    listeners.add(listener);

    return () => {
        listeners.delete(listener);
    };
}

// Przekazanie zdarzenia wszystkim subskrybentom.
export function publishOrderEvent(
    event: ProductionRealtimeEvent
): void {
    for (const listener of listeners) {
        listener(event);
    }
}

// Sprawdzenie wiadomości otrzymanej z serwera.
export function parseOrderChanged(
    value: unknown
): ProductionOrderChanged | null {
    if (
        typeof value !== "object" ||
        value === null
    ) {
        return null;
    }

    const data = value as Record<string, unknown>;

    if (
        typeof data.orderId !== "string" ||
        typeof data.workCenterId !== "string" ||
        typeof data.changeType !== "string" ||
        typeof data.status !== "string" ||
        typeof data.occurredAtUtc !== "string"
    ) {
        return null;
    }

    return {
        orderId: data.orderId,
        workCenterId: data.workCenterId,
        changeType: data.changeType,
        status: data.status,
        occurredAtUtc: data.occurredAtUtc
    };
}
