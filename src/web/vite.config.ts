import { fileURLToPath } from "node:url"
import { defineConfig } from "vitest/config"
import react from "@vitejs/plugin-react"
import tailwindcss from "@tailwindcss/vite"
import { coverageScope } from "../../tools/web/web-coverage-scope.ts"

// locales/ lives at the repository root, not under src/web (see
// tools/i18n/check-locales.ts and .github/workflows/i18n-translate.yml).
// src/web/src/i18n/catalogueFor.ts reaches it with a relative
// import.meta.glob; server.fs.allow lets the dev server serve a path outside
// its own project root.
const repoRoot = fileURLToPath(new URL("../..", import.meta.url))

export default defineConfig({
	plugins: [react(), tailwindcss()],
	test: {
		environment: "jsdom",
		include: ["src/**/*.test.{ts,tsx}"],
		coverage: {
			provider: "v8",
			// What is held to 100% is decided by the files on disk, not a list
			// edited here (ADR-0188): a Foo.tsx with a sibling Foo.view.tsx, and
			// a helper with a colocated test. A view, main.tsx, routes.tsx, a
			// test and a declaration file are never in scope. A file in scope
			// that no test loads still counts, as 0%.
			include: coverageScope(fileURLToPath(new URL(".", import.meta.url))),
			exclude: ["**/*.view.tsx", "src/main.tsx", "src/routes.tsx", "**/*.test.*", "**/*.d.ts"],
			// The lcov names each file from the repository root
			// (src/web/src/lib/sortChoices.ts), as the .NET and tools/ reports do,
			// so the merged report finds the source (#769).
			reporter: ["text", ["lcov", { projectRoot: repoRoot }]],
			// tools/dev/ci-local.sh and the CI `test` job read the lcov from here,
			// the path the coverage job merges (ci.yml "Merge into one report").
			// tools/web/check-web-lcov.ts proves it in the `web` job.
			reportsDirectory: fileURLToPath(new URL("../../artifacts/coverage/web", import.meta.url)),
			thresholds: { lines: 100, branches: 100, functions: 100, statements: 100, perFile: true },
		},
	},
	server: {
		host: true,
		// Named hosts the dev server will answer to, beyond localhost. Vite
		// refuses an unknown Host header, so reaching the dev server by machine
		// name on a LAN needs it listed here. `preview` (tests/e2e's Playwright
		// webServer) inherits this list, which is how REQ-WLD-030/031 (#463)
		// exercise the two production hostnames and a staging-shaped
		// *.cloudfront.net address against a real Host header, without any real
		// DNS or TLS.
		allowedHosts: ["strider.local", "safety.hpac.ca", "securite.acvl.ca", ".cloudfront.net"],
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
