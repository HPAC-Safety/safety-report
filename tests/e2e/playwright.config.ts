import { execSync } from "node:child_process"
import { defineConfig, devices } from "@playwright/test"
import { defineBddConfig } from "playwright-bdd"

// tests/e2e has no dependency on src/web's package.json (a separate npm
// project, per the repo's per-tool-dir convention — see tools/gherkin), so
// the web server installs and builds src/web itself rather than assuming
// its node_modules already exist.

// Every run serves and tests on its own port, never a fixed default (#675).
// tools/dev/ci-local.sh puts act's jobs on the Docker VM's host network, so two
// concurrent runs (different worktrees, or a local `CI=1 npm test` next to
// one) would otherwise collide on a fixed port, or one would test the
// other's build. E2E_PORT lets a caller pin one; otherwise a free port is
// picked here, once, synchronously, in this file's first evaluation — the
// main process, before Playwright forks workers — by asking a throwaway
// Node process to bind port 0 (the OS hands back a free one) and print it.
// Workers re-evaluate this file but inherit process.env from the main
// process that forked them, so E2E_PORT is already set by the time they
// read it and they reuse it rather than picking their own; a port chosen
// per evaluation would differ between workers, and each would serve and
// test against a different build. Both webServer and baseURL use it below.
if (!process.env.E2E_PORT) {
	process.env.E2E_PORT = execSync(
		"node -e \"const s=require('node:net').createServer();s.listen(0,()=>{process.stdout.write(String(s.address().port));s.close()})\"",
	)
		.toString()
		.trim()
}
const E2E_PORT = process.env.E2E_PORT

// @ui-tagged scenarios in .spec/features/**/*.feature execute here, not through
// Reqnroll — see ADR-0053. playwright-bdd reads the same .feature files in
// place (no copy) and generates runnable specs from them plus the step
// definitions in ./steps; `npm test` runs `bddgen` before `playwright test`
// so the generated specs exist when Playwright collects tests. Filtered to
// `@ui and not @ignore`: an @ignore'd @ui scenario has no step definitions
// yet, same convention ADR-0049 uses for Reqnroll.
const bddTestDir = defineBddConfig({
	featuresRoot: "../../.spec/features",
	features: "../../.spec/features/**/*.feature",
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
		baseURL: `http://localhost:${E2E_PORT}`,
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
			use: {
				...devices["Desktop Chrome"],
				// REQ-WLD-030/031 (#463) navigate to the production hostnames and
				// one cloudfront.net stand-in for an unrecognized host, to exercise
				// hostname-based locale selection without touching real DNS or TLS.
				launchOptions: {
					args: [
						"--host-resolver-rules=MAP safety.hpac.ca 127.0.0.1,MAP securite.acvl.ca 127.0.0.1,MAP d1x2y3.cloudfront.net 127.0.0.1",
					],
				},
			},
		},
	],
	webServer: {
		command: `npm --prefix ../../src/web ci && npm --prefix ../../src/web run build && npm --prefix ../../src/web run preview -- --port ${E2E_PORT} --strictPort`,
		url: `http://localhost:${E2E_PORT}`,
		reuseExistingServer: !process.env.CI,
		timeout: 120_000,
	},
})
