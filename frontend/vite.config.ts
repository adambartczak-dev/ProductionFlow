
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
    plugins: [react()],

    server: {
        // Wykluczamy pliki indeksu Visual Studio.
        watch: {
            ignored: ["**/.vs/**"]
        },

        proxy: {
            // Dotychczasowe REST API.
            "/api": {
                target: "http://127.0.0.1:5041",
                changeOrigin: true
            },

            // NOWE: SignalR oraz WebSocket.
            "/hubs": {
                target: "http://127.0.0.1:5041",
                changeOrigin: true,
                ws: true
            }
        }
    }
});
