import { defineConfig, devices } from "@playwright/test"

// tests/e2e has no dependency on src/web's package.json (a separate npm
// project, per the repo's per-tool-dir convention — see tools/gherkin), so
// the web server installs and builds src/web itself rather than assuming
// its node_modules already exist.
export default defineConfig({
	testDir: ".",
	fullyParallel: true,
	forbidOnly: !!process.env.CI,
	retries: process.env.CI ? 2 : 0,
	reporter: "list",
	use: {
		// Vite's preview server binds the "localhost" hostname, which
		// resolves to the IPv6 loopback here and refuses IPv4 connections on
		// 127.0.0.1 — use the same hostname vite prints, not the IPv4 literal.
		baseURL: "http://localhost:4173",
		trace: "on-first-retry",
	},
	projects: [
		{
			name: "chromium",
			use: { ...devices["Desktop Chrome"] },
		},
	],
	webServer: {
		command:
			"npm --prefix ../../src/web ci && npm --prefix ../../src/web run build && npm --prefix ../../src/web run preview -- --port 4173 --strictPort",
		url: "http://localhost:4173",
		reuseExistingServer: !process.env.CI,
		timeout: 120_000,
	},
})
