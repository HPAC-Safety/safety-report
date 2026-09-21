import { fileURLToPath } from "node:url"
import { defineConfig } from "vite"
import react from "@vitejs/plugin-react"
import tailwindcss from "@tailwindcss/vite"

// locales/ lives at the repository root, not under src/web (see
// tools/check-locales.mjs and .github/workflows/i18n-translate.yml).
// src/web/src/i18n/loadCatalogue.ts reaches it with a relative
// import.meta.glob; server.fs.allow lets the dev server serve a path outside
// its own project root.
const repoRoot = fileURLToPath(new URL("../..", import.meta.url))

export default defineConfig({
	plugins: [react(), tailwindcss()],
	server: {
		host: true,
		// Named hosts the dev server will answer to, beyond localhost. Vite
		// refuses an unknown Host header, so reaching the dev server by machine
		// name on a LAN needs it listed here.
		allowedHosts: ["strider.local"],
		// The admin screens call the API on the same origin, so there is no CORS
		// configuration to get wrong in production and none to weaken in
		// development. HPAC_API_ORIGIN covers running the API outside the
		// compose network.
		proxy: {
			"/api": {
				target: process.env.HPAC_API_ORIGIN ?? "http://localhost:5025",
				changeOrigin: true,
			},
		},
		fs: {
			allow: [repoRoot],
		},
		hmr: {
			clientPort: 5173,
		},
		// locales/ sits outside src/web, reached only through fs.allow above.
		// On a Docker bind mount (docker-compose.yml's `.:/repo`), native fs
		// events for paths outside the project root aren't reliable, so the
		// dev server can keep serving a stale catalogue after a locale file
		// changes until restarted. Polling avoids depending on those events.
		watch: {
			usePolling: true,
		},
	},
})
