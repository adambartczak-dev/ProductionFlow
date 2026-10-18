
import { useEffect, useState } from "react";

import {
    subscribeToOrderEvents
} from "./orderEvents";

// Opcjonalny orderId ogranicza obserwację
// do jednego zlecenia.
export function useOrdersLiveRevision(
    orderId?: string
): number {
    const [revision, setRevision] = useState(0);

    const watchedOrderId =
        orderId?.toLowerCase();

    useEffect(() => {
        return subscribeToOrderEvents(event => {
            // Po odzyskaniu połączenia odświeżamy
            // wszystkie aktualnie widoczne dane.
            if (event.type === "resync") {
                setRevision(value => value + 1);
                return;
            }

            // Strony list reagują na wszystkie zmiany
            // przesłane do danego użytkownika.
            if (!watchedOrderId) {
                setRevision(value => value + 1);
                return;
            }

            // Szczegóły reagują tylko na zmianę
            // wyświetlanego zlecenia.
            if (
                event.order.orderId.toLowerCase() ===
                watchedOrderId
            ) {
                setRevision(value => value + 1);
            }
        });
    }, [watchedOrderId]);

    return revision;
}
