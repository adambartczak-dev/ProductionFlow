
import { useState } from "react";
import type { FormEvent } from "react";
import "./ReportForm.css";

interface ReportFormProps {
    remainingQuantity: number;
    isCompleted: boolean;
    onAddReport: (good: number, scrap: number) => boolean;
    onComplete: () => void;
    onReset: () => void;
}

export default function ReportForm({
    remainingQuantity,
    isCompleted,
    onAddReport,
    onComplete,
    onReset
}: ReportFormProps) {
    const [goodInput, setGoodInput] = useState("0");
    const [scrapInput, setScrapInput] = useState("0");

    const [validationError, setValidationError] =
        useState("");

    const canAddReport =
        !isCompleted && remainingQuantity > 0;

    const canComplete =
        !isCompleted && remainingQuantity === 0;

    // Obsługa wysłania formularza.
    function handleSubmit(
        event: FormEvent<HTMLFormElement>
    ) {
        event.preventDefault();

        const good = Number(goodInput);
        const scrap = Number(scrapInput);

        if (
            !Number.isSafeInteger(good) ||
            !Number.isSafeInteger(scrap) ||
            good < 0 ||
            scrap < 0 ||
            good + scrap === 0
        ) {
            setValidationError(
                "Wpisz nieujemne liczby całkowite, " +
                "w sumie co najmniej 1."
            );
            return;
        }

        setValidationError("");

        // Przekazujemy dane do komponentu App.
        const accepted = onAddReport(good, scrap);

        if (accepted) {
            setGoodInput("0");
            setScrapInput("0");
        }
    }

    // Reset formularza i przykładowego zlecenia.
    function handleReset() {
        setGoodInput("0");
        setScrapInput("0");
        setValidationError("");

        onReset();
    }

    return (
        <section className="card report-card">
            <h3>Nowy raport wykonania</h3>

            <p className="report-description">
                Dodaj dobre sztuki i braki do zlecenia.
            </p>

            <form onSubmit={handleSubmit}>
                <div className="report-fields">
                    <div className="report-field">
                        <label htmlFor="good-input">
                            Dobre sztuki
                        </label>

                        <input
                            id="good-input"
                            type="number"
                            min="0"
                            step="1"
                            required
                            value={goodInput}
                            disabled={!canAddReport}
                            onChange={(event) =>
                                setGoodInput(event.target.value)
                            }
                        />
                    </div>

                    <div className="report-field">
                        <label htmlFor="scrap-input">
                            Braki
                        </label>

                        <input
                            id="scrap-input"
                            type="number"
                            min="0"
                            step="1"
                            required
                            value={scrapInput}
                            disabled={!canAddReport}
                            onChange={(event) =>
                                setScrapInput(event.target.value)
                            }
                        />
                    </div>
                </div>

                {validationError && (
                    <p
                        className="form-message form-message--error"
                        role="alert"
                    >
                        {validationError}
                    </p>
                )}

                <div className="report-actions">
                    <button
                        type="submit"
                        className="action-button"
                        disabled={!canAddReport}
                    >
                        Dodaj raport
                    </button>

                    <button
                        type="button"
                        className="action-button"
                        disabled={!canComplete}
                        onClick={onComplete}
                    >
                        Zakończ zlecenie
                    </button>

                    <button
                        type="button"
                        className="action-button action-button--secondary"
                        onClick={handleReset}
                    >
                        Resetuj demo
                    </button>
                </div>
            </form>
        </section>
    );
}
