
import { useCallback, useEffect, useState } from "react";

import {
    Link,
    NavLink,
    Navigate,
    Route,
    Routes
} from "react-router";

import DashboardPage from "./pages/DashboardPage";
import ProductsPage from "./pages/ProductsPage";
import NewOrderPage from "./pages/NewOrderPage";
import LoginPage from "./pages/LoginPage";
import OrdersPage from "./pages/OrdersPage";
import OrderDetailsPage from "./pages/OrderDetailsPage";
import OperatorOrdersPage from "./pages/OperatorOrdersPage";

import {
    getCurrentUser,
    login,
    logout
} from "./services/authApi";

import type { AuthUser } from "./types";

import {
    canPlanOrders,
    canReadOrder,
    canUseOperatorList
} from "./utils/orderPermissions";

import "./App.css";
import "./navigation.css";
import "./auth.css";

function App() {
    const [user, setUser] =
        useState<AuthUser | null>(null);

    const [isCheckingSession, setIsCheckingSession] =
        useState(true);

    const [sessionError, setSessionError] =
        useState<string | null>(null);

    const [sessionCheckKey, setSessionCheckKey] =
        useState(0);

    const [isLoggingOut, setIsLoggingOut] =
        useState(false);

    const [logoutError, setLogoutError] =
        useState<string | null>(null);

    // Sprawdzenie sesji po uruchomieniu.
    useEffect(() => {
        let active = true;

        async function checkSession() {
            try {
                const currentUser = await getCurrentUser();

                if (active) {
                    setUser(currentUser);
                }
            }
            catch {
                if (active) {
                    setSessionError(
                        "Nie można sprawdzić sesji. " +
                        "Sprawdź, czy API działa."
                    );
                }
            }
            finally {
                if (active) {
                    setIsCheckingSession(false);
                }
            }
        }

        void checkSession();

        return () => {
            active = false;
        };
    }, [sessionCheckKey]);

    // Sprawdzenie sesji po powrocie do karty.
    useEffect(() => {
        if (!user) return;

        let active = true;

        async function checkAfterFocus() {
            try {
                const currentUser = await getCurrentUser();

                if (active) {
                    setUser(currentUser);
                }
            }
            catch {
                // Błąd sieci nie musi oznaczać
                // wygaśnięcia sesji.
            }
        }

        window.addEventListener(
            "focus",
            checkAfterFocus
        );

        return () => {
            active = false;

            window.removeEventListener(
                "focus",
                checkAfterFocus
            );
        };
    }, [user]);

    // Logowanie.
    async function handleLogin(
        email: string,
        password: string
    ) {
        setUser(await login(email, password));
    }

    // Wylogowanie.
    async function handleLogout() {
        setLogoutError(null);
        setIsLoggingOut(true);

        try {
            await logout();
            setUser(null);
        }
        catch (caught) {
            setLogoutError(
                caught instanceof Error
                    ? caught.message
                    : "Błąd wylogowania."
            );
        }
        finally {
            setIsLoggingOut(false);
        }
    }

    // Wygaśnięcie sesji.
    const handleSessionExpired = useCallback(
        () => setUser(null),
        []
    );

    function retrySessionCheck() {
        setSessionError(null);
        setIsCheckingSession(true);

        setSessionCheckKey(value => value + 1);
    }

    if (isCheckingSession) {
        return (
            <main
                className="session-screen"
                role="status"
            >
                Sprawdzanie sesji użytkownika...
            </main>
        );
    }

    if (sessionError) {
        return (
            <main className="session-screen">
                <p role="alert">
                    {sessionError}
                </p>

                <button onClick={retrySessionCheck}>
                    Spróbuj ponownie
                </button>
            </main>
        );
    }

    if (!user) {
        return (
            <Routes>
                <Route
                    path="/login"
                    element={
                        <LoginPage
                            onLogin={handleLogin}
                        />
                    }
                />

                <Route
                    path="*"
                    element={
                        <Navigate
                            to="/login"
                            replace
                        />
                    }
                />
            </Routes>
        );
    }

    // Uprawnienia pobieramy ze wspólnych funkcji.
    const canPlan = canPlanOrders(user);
    const isOperator = canUseOperatorList(user);

    const navClass = ({
        isActive
    }: {
        isActive: boolean
    }) => isActive
            ? "nav-link nav-link--active"
            : "nav-link";

    return (
        <div className="app">
            <header className="site-header">
                <div className="header-inner">
                    <div>
                        <h1>ProductionFlow</h1>
                        <p>Panel zarządzania produkcją</p>
                    </div>

                    <div className="auth-user-panel">
                        <div className="auth-user-details">
                            <strong>{user.displayName}</strong>
                            <span>{user.roles.join(", ")}</span>
                        </div>

                        <button
                            className="logout-button"
                            type="button"
                            onClick={() => void handleLogout()}
                            disabled={isLoggingOut}
                        >
                            {isLoggingOut
                                ? "Wylogowywanie..."
                                : "Wyloguj"}
                        </button>
                    </div>
                </div>
            </header>

            <nav
                className="main-navigation"
                aria-label="Menu główne"
            >
                <div className="navigation-inner">
                    <NavLink
                        to="/"
                        end
                        className={navClass}
                    >
                        Panel główny
                    </NavLink>

                    <NavLink
                        to="/products"
                        className={navClass}
                    >
                        Produkty
                    </NavLink>

                    {canPlan && (
                        <NavLink
                            to="/orders"
                            end
                            className={navClass}
                        >
                            Zlecenia
                        </NavLink>
                    )}

                    {canPlan && (
                        <NavLink
                            to="/orders/new"
                            className={navClass}
                        >
                            Nowe zlecenie
                        </NavLink>
                    )}

                    {isOperator && (
                        <NavLink
                            to="/operator/orders"
                            className={navClass}
                        >
                            Moje zlecenia
                        </NavLink>
                    )}
                </div>
            </nav>

            <main className="main-content">
                {logoutError && (
                    <p
                        className="auth-error"
                        role="alert"
                    >
                        {logoutError}
                    </p>
                )}

                <Routes>
                    <Route
                        path="/"
                        element={
                            <DashboardPage user={user} />
                        }
                    />

                    <Route
                        path="/products"
                        element={
                            <ProductsPage
                                onSessionExpired={
                                    handleSessionExpired
                                }
                            />
                        }
                    />

                    <Route
                        path="/orders"
                        element={
                            canPlan ? (
                                <OrdersPage
                                    onSessionExpired={
                                        handleSessionExpired
                                    }
                                />
                            ) : (
                                <p>
                                    Brak uprawnień do globalnej
                                    listy zleceń.
                                </p>
                            )
                        }
                    />

                    <Route
                        path="/orders/new"
                        element={
                            canPlan ? (
                                <NewOrderPage
                                    onSessionExpired={
                                        handleSessionExpired
                                    }
                                />
                            ) : (
                                <p>
                                    Brak uprawnień do tworzenia zleceń.
                                </p>
                            )
                        }
                    />

                    <Route
                        path="/operator/orders"
                        element={
                            isOperator ? (
                                <OperatorOrdersPage
                                    onSessionExpired={
                                        handleSessionExpired
                                    }
                                />
                            ) : (
                                <p>
                                    Brak uprawnień do listy operatora.
                                </p>
                            )
                        }
                    />

                    <Route
                        path="/orders/:id"
                        element={
                            canReadOrder(user) ? (
                                <OrderDetailsPage
                                    user={user}
                                    onSessionExpired={
                                        handleSessionExpired
                                    }
                                />
                            ) : (
                                <p>
                                    Brak uprawnień do szczegółów
                                    zlecenia.
                                </p>
                            )
                        }
                    />

                    <Route
                        path="/login"
                        element={
                            <Navigate to="/" replace />
                        }
                    />

                    <Route
                        path="*"
                        element={
                            <section className="card">
                                <h2>Nie znaleziono strony</h2>

                                <Link
                                    to="/"
                                    className="return-link"
                                >
                                    Wróć do panelu głównego
                                </Link>
                            </section>
                        }
                    />
                </Routes>
            </main>
        </div>
    );
}

export default App;
