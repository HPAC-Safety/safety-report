import { createBdd } from "playwright-bdd"
import { expect } from "@playwright/test"

const { Given, When, Then } = createBdd()

interface LocaleSignal {
	storedLocale?: string
	browserLanguages?: string[]
}

let pendingSignal: LocaleSignal = {}

function localeCodeIn(text: string): string | undefined {
	return text.match(/[a-z]{2}-[A-Z]{2}/)?.[0]
}

Given(/^a visitor has ((?:an explicit stored language choice|no stored choice).+)$/, async ({}, signal: string) => {
	if (signal.startsWith("an explicit stored language choice of")) {
		pendingSignal = { storedLocale: localeCodeIn(signal) }
	} else if (signal.startsWith("no stored choice but a supported browser language of")) {
		pendingSignal = { browserLanguages: [localeCodeIn(signal)!] }
	} else {
		pendingSignal = { browserLanguages: ["de-DE"] }
	}
})

When("the page loads", async ({ page, context }) => {
	const { storedLocale, browserLanguages } = pendingSignal
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
	await page.goto("/")
})

Then(/^the locale (.+) is selected$/, async ({ page }, chosen: string) => {
	const expected = localeCodeIn(chosen) ?? "en-CA"
	await expect(page.locator("html")).toHaveAttribute("lang", expected)
})

Given("a visitor is on any page", async ({ page }) => {
	await page.goto("/")
})

When("the visitor switches the language toggle", async ({ page }) => {
	await page.getByRole("button", { name: "Language" }).click()
})

Then("the document lang attribute and page title update", async ({ page }) => {
	await expect(page.locator("html")).not.toHaveAttribute("lang", "en-CA")
})

Then("the language choice persists to local storage across a reload", async ({ page }) => {
	const stored = await page.evaluate(() => localStorage.getItem("hpac.locale"))
	expect(stored).toBe("fr-CA")

	await page.reload()
	await expect(page.locator("html")).toHaveAttribute("lang", "fr-CA")
})
