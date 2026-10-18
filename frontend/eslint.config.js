
import js from "@eslint/js";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";
import { defineConfig, globalIgnores } from "eslint/config";

export default defineConfig([
    globalIgnores([
        "dist",
        "playwright-report",
        "test-results"
    ]),

    // Dotychczasowa konfiguracja TypeScript i React.
    {
        files: ["**/*.{ts,tsx}"],
        extends: [
            js.configs.recommended,
            tseslint.configs.recommended,
            reactHooks.configs.flat.recommended,
            reactRefresh.configs.vite
        ],
        languageOptions: {
            globals: globals.browser
        }
    },

    // Dodatkowe zmienne globalne dla środowiska Node.js.
    {
        files: [
            "playwright.config.ts",
            "e2e/**/*.{ts,tsx}"
        ],
        languageOptions: {
            globals: globals.node
        }
    }
]);
