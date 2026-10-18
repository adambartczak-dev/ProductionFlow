
import { defineConfig, devices } from "@playwright/test";
import { config as loadEnv } from "dotenv";

// Lokalne hasła kont demonstracyjnych.
loadEnv({
    path: ".env.e2e.local"
});

// Wybór środowiska testowego.
const dockerMode = process.env.PF_E2E_TARGET === "docker";

const baseURL = dockerMode
    ? "http://127.0.0.1:3000"
    : "http://127.0.0.1:5173";

export default defineConfig({
    testDir: "./e2e",

    fullyParallel: false,
    workers: 1,
    retries: 0,

    reporter: [
        ["list"],
        ["html", { open: "never" }]
    ],

    use: {
        baseURL,
        screenshot: "only-on-failure",
        trace: "retain-on-failure"
    },

    projects: [
        {
            name: "chromium",
            use: {
                ...devices["Desktop Chrome"]
            }
        }
    ],

    // W trybie Docker Compose sam uruchamia frontend.
    // W zwykłym trybie Playwright uruchamia Vite.
    webServer: dockerMode
        ? undefined
        : {
            command:
                "npm run dev -- --host 127.0.0.1 --port 5173 --strictPort",

            url: "http://127.0.0.1:5173",

            reuseExistingServer: true,

            timeout: 120000
        }
});
