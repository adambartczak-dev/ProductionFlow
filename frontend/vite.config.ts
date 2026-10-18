
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
    plugins: [react()],

    server: {
        // Nie obserwujemy plików technicznych Visual Studio.
        watch: {
            ignored: ["**/.vs/**"]
        },

        proxy: {
            "/api": {
                target: "http://127.0.0.1:5041",
                changeOrigin: true
            }
        }
    }
});
