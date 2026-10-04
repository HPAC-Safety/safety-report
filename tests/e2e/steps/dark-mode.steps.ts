import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"
import AxeBuilder from "@axe-core/playwright"

import { signInAs, stubAuth } from "./auth"
import { stubReports } from "./manage-reports.steps"
import { stubFeed } from "./public-reports.steps"
import { defaultFormQuestions, forgetDraftInBrowser, stubCurrentQuestions } from "./report-form-fixture"
import { present } from "./present"

const { Given, When, Then } = createBdd()

/*
 * REQ-WLD-022: the dark theme, in both languages, on the form, the form
 * showing errors, the public feed, and the admin report list. The API is
 * stubbed at the network boundary; what is asserted is what axe finds in the
 * rendered page and what the focused control looks like.
 */

Given(
	/^a visitor's stored theme preference is dark and their language is (en-CA|fr-CA)$/,
	async ({ context }, locale: string) => {
		await context.addInitScript((chosen) => {
			localStorage.setItem("hpac.theme", "dark")
			localStorage.setItem("hpac.locale", chosen)
		}, locale)
	},
)

When(/^they open (the report form|the report form showing its errors|the public feed|the admin report list)$/, async ({ page }, target: string) => {
	if (target === "the public feed") {
		await stubAuth(page)
		await stubFeed(page)
		await page.goto("/reports")
		await expect(page.getByRole("list").getByRole("listitem").first()).toBeVisible()
	} else if (target === "the admin report list") {
		await stubReports(page)
		await signInAs(page, "safety_officer")
		await page.goto("/admin/reports")
		await expect(page.locator("[data-report-id]").first()).toBeVisible()
	} else {
		const questions = defaultFormQuestions()
		present(questions.find((q) => q.key === "narrative")).isRequired = true
		await stubCurrentQuestions(page, questions)
		await signInAs(page, "user")
		await page.goto("/report")
		await forgetDraftInBrowser(page)
		await page.reload()
		await expect(page.getByRole("heading", { level: 1 })).toBeVisible()

		if (target === "the report form showing its errors") {
			const next = page.getByRole("button", { name: /^(Next|Suivant)$/ })
			await next.click()
			await next.click()
			await expect(page.getByRole("alert").first()).toBeVisible()
		}
	}

	await expect(page.locator("html")).toHaveAttribute("data-theme", "dark")
})

// Brand red set as text is exempt by owner decision (ADR-0024 amendment): the
// one token class that carries it, and nothing else, is excluded from the scan.
const BRAND_RED_TEXT = ".text-brand-700"

/**
 * Waits until every running CSS transition has finished. The form fades in
 * over 200 ms, and axe reads the colour a transition has reached so far, so a
 * scan taken mid-fade blends the text with the page behind it and reports
 * contrast the settled page does not have. Only transitions are awaited: an
 * infinite animation never finishes.
 */
async function settleTransitions(page: Page) {
	await page.evaluate(() => Promise.allSettled(document.getAnimations().filter((animation) => animation instanceof CSSTransition).map((animation) => animation.finished)))
}

Then(/^an accessibility scan reports no color-contrast violation, except for text set in the HPAC brand red.*$/, async ({ page }) => {
	await settleTransitions(page)
	const results = await new AxeBuilder({ page }).withRules(["color-contrast"]).exclude(BRAND_RED_TEXT).analyze()
	const summary = results.violations.flatMap((violation) =>
		violation.nodes.map((node) => `${node.target.join(" ")}: ${node.any[0]?.message ?? violation.help}`),
	)
	expect(summary).toEqual([])
})

Then("the focused control shows a visible focus indicator", async ({ page }) => {
	await focusFirstControl(page)
	await settleTransitions(page)

	const indicator = await page.evaluate(() => {
		if (!document.activeElement) throw new Error("No element has focus.")
		const style = getComputedStyle(document.activeElement)
		return { outlineStyle: style.outlineStyle, outlineWidth: parseFloat(style.outlineWidth), boxShadow: style.boxShadow }
	})
	const hasOutline = indicator.outlineStyle !== "none" && indicator.outlineWidth > 0
	const hasShadow = indicator.boxShadow !== "none"
	expect(hasOutline || hasShadow).toBe(true)
})

/** Tabs until a control inside the page body, not the header, holds focus. */
async function focusFirstControl(page: Page) {
	for (let press = 0; press < 40; press += 1) {
		await page.keyboard.press("Tab")
		const inMain = await page.evaluate(() => !!document.activeElement?.closest("main"))
		if (inMain) return
	}
	throw new Error("No control inside main took focus.")
}
