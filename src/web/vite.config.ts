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
