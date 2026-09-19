import { defineConfig, devices } from "@playwright/test"
import { defineBddConfig } from "playwright-bdd"

// tests/e2e has no dependency on src/web's package.json (a separate npm
// project, per the repo's per-tool-dir convention — see tools/gherkin), so
// the web server installs and builds src/web itself rather than assuming
// its node_modules already exist.

// @ui-tagged scenarios in features/**/*.feature execute here, not through
// Reqnroll — see ADR-0053. playwright-bdd reads the same .feature files in
// place (no copy) and generates runnable specs from them plus the step
// definitions in ./steps; `npm test` runs `bddgen` before `playwright test`
// so the generated specs exist when Playwright collects tests. Filtered to
// `@ui and not @ignore`: an @ignore'd @ui scenario has no step definitions
// yet, same convention ADR-0049 uses for Reqnroll.
const bddTestDir = defineBddConfig({
	featuresRoot: "../../features",
	features: "../../features/**/*.feature",
	steps: "steps/**/*.ts",
	tags: "@ui and not @ignore",
})

export default defineConfig({
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
			testDir: ".",
			testIgnore: "**/.features-gen/**",
			use: { ...devices["Desktop Chrome"] },
		},
		{
			name: "chromium-bdd",
			testDir: bddTestDir,
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
