import { createBdd } from "playwright-bdd"
import { expect } from "@playwright/test"
import { present } from "./present"

const { Given, When, Then } = createBdd()

interface LocaleSignal {
	storedLocale?: string
	browserLanguages?: string[]
	hostname?: string
}

let pendingSignal: LocaleSignal = {}

function localeCodeIn(text: string): string | undefined {
	return text.match(/[a-z]{2}-[A-Z]{2}/)?.[0]
}

// The hostnames this suite navigates to (production's two, plus a
// cloudfront.net stand-in for an unrecognized host) are mapped to loopback by
// playwright.config.ts's --host-resolver-rules, so no real DNS or TLS is
// needed.
function hostnameIn(text: string): string | undefined {
	return text.match(/visiting ([\w.-]+)/)?.[1]
}

// playwright.config.ts sets E2E_PORT before this file is imported, either to
// the caller's pinned value or to a free port it picked itself (#675) — never
// a fixed default, so a hostname navigation here lands on the same port
// baseURL and webServer use.
const e2ePort = process.env.E2E_PORT

// eslint-disable-next-line no-empty-pattern -- Playwright reads the fixtures a step needs from this destructuring pattern, and this one needs none
Given(/^a visitor has ((?:an explicit stored language choice|no stored choice).+)$/, ({}, signal: string) => {
	const hostname = hostnameIn(signal)
	if (signal.startsWith("an explicit stored language choice of")) {
		pendingSignal = { storedLocale: localeCodeIn(signal), hostname }
	} else if (signal.startsWith("no stored choice but a supported browser language of")) {
		pendingSignal = { browserLanguages: [present(localeCodeIn(signal))], hostname }
	} else if (hostname) {
		pendingSignal = { hostname }
	} else {
		pendingSignal = { browserLanguages: ["de-DE"] }
	}
})

When("the page loads", async ({ page, context }) => {
	const { storedLocale, browserLanguages, hostname } = pendingSignal
	await context.addInitScript(
		([stored, languages]) => {
			if (stored) localStorage.setItem("hpac.locale", stored)
			if (languages) {
				Object.defineProperty(window.navigator, "languages", { get: () => languages, configurable: true })
				Object.defineProperty(window.navigator, "language", { get: () => languages[0], configurable: true })
			}
		},
		[storedLocale ?? null, browserLanguages ?? null] as const,
	)
	await page.goto(hostname ? `http://${hostname}:${e2ePort}/` : "/")
})

Given(/^a visitor loads the page at ([\w.-]+)$/, async ({ page }, hostname: string) => {
	await page.goto(`http://${hostname}:${e2ePort}/`)
})

Then(/^the browser stays on ([\w.-]+)$/, ({ page }, hostname: string) => {
	expect(new URL(page.url()).hostname).toBe(hostname)
})

Then(/^the locale (.+) is selected$/, async ({ page }, chosen: string) => {
	const expected = localeCodeIn(chosen) ?? "en-CA"
	await expect(page.locator("html")).toHaveAttribute("lang", expected)
})

Given("a visitor is on any page", async ({ page }) => {
	await page.goto("/")
})

// The toggle's accessible name is locale-dependent ("Switch to …" in English,
// "Passer à …" in French per locales/fr-CA.json), which matters here because
// REQ-WLD-031 loads the page on a French-defaulting hostname.
When("the visitor switches the language toggle", async ({ page }) => {
	await page.getByRole("button", { name: /^(Switch to|Passer à)/ }).click()
})

Then("the document lang attribute and page title update", async ({ page }) => {
	await expect(page.locator("html")).not.toHaveAttribute("lang", "en-CA")
	await expect(page).not.toHaveTitle("HPAC Safety")
})

Then("the language choice persists to local storage across a reload", async ({ page }) => {
	const stored = await page.evaluate(() => localStorage.getItem("hpac.locale"))
	expect(stored).toBe("fr-CA")

	await page.reload()
	await expect(page.locator("html")).toHaveAttribute("lang", "fr-CA")
})

// Shared by the scenarios that read a stored value in the chosen language
// (REQ-MOD-075, REQ-MOD-076, REQ-SUB-068): the choice a visitor made with the
// language toggle, in place before the first page load.
Given(/^the interface language is (English|French)$/, async ({ context }, language: string) => {
	await context.addInitScript((locale) => localStorage.setItem("hpac.locale", locale), language === "French" ? "fr-CA" : "en-CA")
})

// REQ-WLD-049. A deploy deletes the previous build's hashed files, and
// CloudFront answers a missing one with index.html at 200 (infra/cdn.tf's SPA
// fallback). Routing every later /assets/ request to that same answer is
// what an open tab meets after a deploy.
Given("a visitor has the page open in English", async ({ page, context }) => {
	await context.addInitScript(() => localStorage.setItem("hpac.locale", "en-CA"))
	await page.goto("/")
	await expect(page).toHaveTitle("HPAC Safety")
})

Given("a deploy has since removed every script file the page has not loaded", async ({ page }) => {
	await page.route("**/assets/**", (route) =>
		route.fulfill({ status: 200, contentType: "text/html", body: "<!doctype html><html><body></body></html>" }),
	)
})

Then("the page shows its French interface text, not catalogue keys", async ({ page }) => {
	await expect(page.locator("html")).toHaveAttribute("lang", "fr-CA")
	await expect(page).toHaveTitle("ACVL Sécurité")
	await expect(page.getByRole("button", { name: /^Passer à/ })).toBeVisible()
	await expect(page.locator("body")).not.toContainText("footer.copyright")
})
