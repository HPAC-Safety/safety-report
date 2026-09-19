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
		fs: {
			allow: [repoRoot],
		},
	},
})
