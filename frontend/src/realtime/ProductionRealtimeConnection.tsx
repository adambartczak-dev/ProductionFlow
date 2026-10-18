
import { useEffect } from "react";

import {
    HubConnectionBuilder,
    HubConnectionState,
    LogLevel
} from "@microsoft/signalr";

import {
    parseOrderChanged,
    publishOrderEvent
} from "./orderEvents";

interface ProductionRealtimeConnectionProps {
    userId: string;
}

const RETRY_DELAY_MS = 5000;

export default function ProductionRealtimeConnection({
    userId
}: ProductionRealtimeConnectionProps) {
    useEffect(() => {
        let disposed = false;

        let retryTimer:
            ReturnType<typeof setTimeout> | undefined;

        // SignalR korzysta z istniejącej sesji cookie Identity.
        const connection = new HubConnectionBuilder()
            .withUrl("/hubs/production", {
                withCredentials: true
            })
            .withAutomaticReconnect([
                0,
                2000,
                10000,
                30000
            ])
            .configureLogging(LogLevel.Warning)
            .build();

        // Odbiór powiadomień z backendu.
        connection.on("OrderChanged", (data: unknown) => {
            if (disposed) {
                return;
            }

            const order = parseOrderChanged(data);

            if (!order) {
                console.warn(
                    "SignalR: nieprawidłowy format OrderChanged."
                );
                return;
            }

            console.info(
                "SignalR: zmiana zlecenia",
                order.orderId,
                order.changeType
            );

            publishOrderEvent({
                type: "changed",
                order
            });
        });

        // Utrata aktywnego połączenia.
        connection.onreconnecting(() => {
            if (!disposed) {
                console.warn(
                    "SignalR: połączenie przerwane. " +
                    "Trwa ponawianie."
                );
            }
        });

        // Po ponownym połączeniu mogliśmy pominąć
        // zdarzenia wysłane w czasie rozłączenia.
        connection.onreconnected(() => {
            if (disposed) {
                return;
            }

            console.info(
                "SignalR: przywrócono połączenie."
            );

            publishOrderEvent({
                type: "resync"
            });
        });

        function scheduleRetry() {
            if (disposed || retryTimer !== undefined) {
                return;
            }

            retryTimer = setTimeout(() => {
                retryTimer = undefined;
                void startConnection();
            }, RETRY_DELAY_MS);
        }

        // Pierwsze połączenie i próby po pełnym rozłączeniu.
        async function startConnection() {
            if (
                disposed ||
                connection.state !==
                HubConnectionState.Disconnected
            ) {
                return;
            }

            try {
                await connection.start();

                if (disposed) {
                    await connection.stop();
                    return;
                }

                console.info(
                    "SignalR: połączono z /hubs/production."
                );

                // Odczyt aktualnego stanu po połączeniu.
                publishOrderEvent({
                    type: "resync"
                });
            }
            catch (error) {
                if (!disposed) {
                    console.warn(
                        "SignalR: nie udało się połączyć.",
                        error
                    );

                    scheduleRetry();
                }
            }
        }

        // Gdy automatyczne ponawianie wyczerpie próby,
        // uruchamiamy ponownie normalny mechanizm start().
        connection.onclose(() => {
            if (!disposed) {
                console.warn(
                    "SignalR: połączenie zamknięte."
                );

                scheduleRetry();
            }
        });

        void startConnection();

        // Wylogowanie, zmiana użytkownika lub
        // odmontowanie komponentu kończy połączenie.
        return () => {
            disposed = true;

            if (retryTimer !== undefined) {
                clearTimeout(retryTimer);
            }

            connection.off("OrderChanged");

            void connection.stop().catch(error => {
                console.warn(
                    "SignalR: błąd zamykania połączenia.",
                    error
                );
            });
        };
    }, [userId]);

    return null;
}
