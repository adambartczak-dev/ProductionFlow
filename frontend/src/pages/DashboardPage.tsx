
import { Link } from "react-router";
import type { AuthUser } from "../types";
import {
    canPlanOrders,
    canUseOperatorList
} from "../utils/orderPermissions";

interface Props {
    user: AuthUser;
}

export default function DashboardPage({ user }: Props) {
    const canPlan = canPlanOrders(user);
    const canSeeMyOrders = canUseOperatorList(user);

    return (
        <>
            <div className="page-intro">
                <p className="eyebrow">PANEL GŁÓWNY</p>
                <h2>Witaj, {user.displayName}</h2>
                <p>
                    Wybierz zadanie odpowiadające Twojej
                    roli w ProductionFlow.
                </p>
            </div>

            <section className="card">
                <h3>Twoje role</h3>
                <p>{user.roles.join(", ") || "Brak roli produkcyjnej"}</p>
                <p>
                    Widoczność funkcji zależy od roli.
                    Backend niezależnie sprawdza prawo dostępu
                    do każdego endpointu.
                </p>
            </section>

            {canPlan && (
                <section
                    className="card"
                    style={{ marginTop: 20 }}
                >
                    <h3>Planowanie produkcji</h3>
                    <p>
                        Przeglądaj, twórz i wydawaj zlecenia
                        do realizacji.
                    </p>
                    <p>
                        <Link to="/orders">
                            Otwórz listę zleceń
                        </Link>
                    </p>
                    <p>
                        <Link to="/orders/new">
                            Utwórz nowe zlecenie
                        </Link>
                    </p>
                </section>
            )}

            {canSeeMyOrders && (
                <section
                    className="card"
                    style={{ marginTop: 20 }}
                >
                    <h3>Realizacja zleceń</h3>
                    <p>
                        Rozpoczynaj zlecenia przypisane do
                        swojego stanowiska i zapisuj raporty.
                    </p>
                    <p>
                        <Link to="/operator/orders">
                            Otwórz moje zlecenia
                        </Link>
                    </p>
                </section>
            )}

            {!canPlan && !canSeeMyOrders && (
                <section
                    className="card"
                    style={{ marginTop: 20 }}
                >
                    <h3>Brak przydzielonych operacji</h3>
                    <p>
                        Skontaktuj się z administratorem
                        w sprawie przypisania roli.
                    </p>
                </section>
            )}
        </>
    );
}
