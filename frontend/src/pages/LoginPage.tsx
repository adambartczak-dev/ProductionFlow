
import { useState } from "react";
import type { FormEvent } from "react";

interface LoginPageProps {
    onLogin: (
        email: string,
        password: string
    ) => Promise<void>;
}

export default function LoginPage({
    onLogin
}: LoginPageProps) {
    const [email, setEmail] =
        useState("planner@productionflow.local");

    const [password, setPassword] = useState("");
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);

    async function handleSubmit(
        event: FormEvent<HTMLFormElement>
    ) {
        event.preventDefault();

        setError(null);
        setIsSubmitting(true);

        try {
            await onLogin(email.trim(), password);
        }
        catch (caughtError) {
            setError(
                caughtError instanceof Error
                    ? caughtError.message
                    : "Nieznany błąd logowania."
            );
        }
        finally {
            setPassword("");
            setIsSubmitting(false);
        }
    }

    return (
        <div className="login-page">
            <header className="login-header">
                <h1>ProductionFlow</h1>
                <p>System zarządzania produkcją</p>
            </header>

            <main className="login-card">
                <h2>Logowanie</h2>
                <p>Wprowadź dane konta demonstracyjnego.</p>

                <form onSubmit={handleSubmit}>
                    <label htmlFor="login-email">
                        Adres e-mail
                    </label>

                    <input
                        id="login-email"
                        type="email"
                        autoComplete="username"
                        value={email}
                        onChange={(event) =>
                            setEmail(event.target.value)
                        }
                        required
                        disabled={isSubmitting}
                    />

                    <label htmlFor="login-password">
                        Hasło
                    </label>

                    <input
                        id="login-password"
                        type="password"
                        autoComplete="current-password"
                        value={password}
                        onChange={(event) =>
                            setPassword(event.target.value)
                        }
                        required
                        disabled={isSubmitting}
                    />

                    {error && (
                        <p className="login-error" role="alert">
                            {error}
                        </p>
                    )}

                    <button
                        type="submit"
                        disabled={isSubmitting}
                    >
                        {isSubmitting
                            ? "Logowanie..."
                            : "Zaloguj się"}
                    </button>
                </form>

                <p className="login-hint">
                    Hasła kont demonstracyjnych są
                    przechowywane w User Secrets backendu.
                </p>
            </main>
        </div>
    );
}
