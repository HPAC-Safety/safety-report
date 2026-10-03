import { createBdd } from "playwright-bdd"
import { expect, type Locator, type Page } from "@playwright/test"

import {
	openReport,
	reportDetail,
	stubReportDetail,
	SUMMARY_EN,
	SUMMARY_FR,
} from "./admin-report-fixture"
import { present } from "./present"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for Markdown (ADR-0180): a summary, and a reporter's paragraph
 * answer with its translation, render as a safe subset of Markdown, and the feed
 * previews a summary's first section as plain text (REQ-WLD-045, REQ-MOD-208 to
 * REQ-MOD-211).
 *
 * The API is stubbed at the network boundary, as for the other pages: what these
 * assert is what the browser shows. Every report and text below is synthetic.
 */

const PUBLIC_ID = "mdpublic001"
const FEED_ID = "mdfeed00001"

// --- REQ-WLD-045: the safe subset ---

interface MarkdownCase {
	markdown: string
	/** The result column of the feature's table, and what proves it. */
	results: Record<string, (summary: Locator) => Promise<void>>
}

/** Requests the page made to the host an image or link would have pointed at. */
const foreignRequests = new WeakMap<Page, string[]>()

const CASES: Record<string, MarkdownCase> = {
	'a "## Description" heading': {
		markdown: "## Description\nThe pilot landed in a field.",
		results: {
			'a level-two heading "Description"': async (summary) => {
				await expect(summary.getByRole("heading", { level: 2, name: "Description" })).toBeVisible()
			},
		},
	},
	"two paragraphs": {
		markdown: "First paragraph of the summary.\n\nSecond paragraph of the summary.",
		results: {
			"two separate paragraphs": async (summary) => {
				await expect(summary.locator("p")).toHaveCount(2)
			},
		},
	},
	"bold and italic words": {
		markdown: "A **firm** landing in *gentle* wind.",
		results: {
			"the words in bold and in italic": async (summary) => {
				await expect(summary.locator("strong")).toHaveText("firm")
				await expect(summary.locator("em")).toHaveText("gentle")
			},
		},
	},
	"a bulleted list of two items": {
		markdown: "- The left side collapsed\n- The reserve was thrown",
		results: {
			"a list with two items": async (summary) => {
				await expect(summary.locator("ul > li")).toHaveCount(2)
			},
		},
	},
	"a line break inside a paragraph": {
		markdown: "The wing collapsed.\nThe pilot threw the reserve.",
		results: {
			"a line break at that point": async (summary) => {
				await expect(summary.locator("p")).toHaveCount(1)
				await expect(summary.locator("p br")).toHaveCount(1)
			},
		},
	},
	'raw HTML "<b>loud</b>"': {
		markdown: "The report said <b>loud</b> and <script>window.__ran = true</script> more.",
		results: {
			'the text "<b>loud</b>" as written, with nothing in bold': async (summary) => {
				await expect(summary).toContainText("<b>loud</b>")
				await expect(summary.locator("b, strong, script")).toHaveCount(0)
			},
		},
	},
	'a link "[club](https://example.test/club)"': {
		markdown: "Read more from the [club](https://example.test/club) today.",
		results: {
			'the word "club" as plain text, not a link': async (summary) => {
				await expect(summary).toContainText("Read more from the club today.")
				await expect(summary.locator("a")).toHaveCount(0)
			},
		},
	},
	'an image "![map](https://example.test/a.png)"': {
		markdown: "The site ![map](https://example.test/a.png) is shown.",
		results: {
			"no image, and no request for it": async (summary) => {
				await expect(summary.locator("img")).toHaveCount(0)
				expect(foreignRequests.get(summary.page())).toEqual([])
			},
		},
	},
}

let chosen: MarkdownCase | null = null

Given("a published report whose summary contains {}", async ({ page }, markdown: string) => {
	chosen = CASES[markdown] ?? null
	expect(chosen, `no case for ${markdown}`).not.toBeNull()

	foreignRequests.set(page, [])
	await page.route("https://example.test/**", async (route) => {
		present(foreignRequests.get(page)).push(route.request().url())
		await route.abort()
	})
	await page.route(/\/api\/v1\/public\/reports\/[^/?]+\/comments\/?$/, (route) => route.fulfill({ json: [] }))
	await page.route(new RegExp(`/api/v1/public/reports/${PUBLIC_ID}$`), (route) =>
		route.fulfill({
			json: {
				id: PUBLIC_ID,
				aiSummaryEn: present(chosen).markdown,
				aiSummaryFr: present(chosen).markdown,
				language: "en-CA",
				publishedAt: "2026-09-20T15:30:00Z",
				commentCount: 0,
				attachmentCount: 0,
				media: [],
			},
		}),
	)
})

When("a visitor opens its page", async ({ page }) => {
	await page.goto(`/reports/${PUBLIC_ID}`)
	await expect(page.locator('[data-summary="en-CA"]')).toBeVisible()
})

Then("the summary is rendered as {}", async ({ page }, result: string) => {
	const check = present(chosen).results[result]
	expect(check, `no check for ${result}`).toBeDefined()
	await check(page.locator('[data-summary="en-CA"]'))
})

// --- REQ-WLD-046: the same type as the other public pages ---

Given("the visitor's system prefers the {word} theme", async ({ page }, theme: string) => {
	await page.emulateMedia({ colorScheme: theme as "light" | "dark" })
})

Given('a published report whose summary has a "## Description" section', async ({ page }) => {
	await page.route(/\/api\/v1\/public\/reports\/[^/?]+\/comments\/?$/, (route) => route.fulfill({ json: [] }))
	await page.route(new RegExp(`/api/v1/public/reports/${PUBLIC_ID}$`), (route) =>
		route.fulfill({
			json: {
				id: PUBLIC_ID,
				aiSummaryEn: "## Description\n\nThe pilot launched in a gusting wind.",
				aiSummaryFr: "## Description\n\nLe pilote a décollé par vent en rafales.",
				language: "en-CA",
				publishedAt: "2026-09-20T15:30:00Z",
				commentCount: 0,
				attachmentCount: 0,
				media: [],
			},
		}),
	)
})

async function typeOf(element: Locator) {
	await expect(element).toBeVisible()
	return element.evaluate((el) => {
		const style = getComputedStyle(el)
		return { color: style.color, fontSize: style.fontSize, fontWeight: style.fontWeight }
	})
}

/** The type of one element of the summary, then of its counterpart on the home page. */
async function summaryAndHome(page: Page, inSummary: (summary: Locator) => Locator, onHome: string) {
	await page.goto(`/reports/${PUBLIC_ID}`)
	const summary = await typeOf(inSummary(page.locator('[data-summary="en-CA"]')).first())
	await page.goto("/")
	const home = await typeOf(page.locator(onHome).first())
	return { summary, home }
}

Then(
	"the summary's paragraph matches the home page's section prose in color, size, and weight",
	async ({ page }) => {
		const { summary, home } = await summaryAndHome(page, (s) => s.locator("p"), "main section h2 + p")
		expect(summary).toEqual(home)
	},
)

Then(
	"the summary's section heading matches the home page's section heading in color, size, and weight",
	async ({ page }) => {
		const { summary, home } = await summaryAndHome(
			page,
			(s) => s.getByRole("heading", { name: "Description" }),
			"main section h2",
		)
		expect(summary).toEqual(home)
	},
)

Then("the summary's section heading is still a level-two heading", async ({ page }) => {
	await page.goto(`/reports/${PUBLIC_ID}`)
	await expect(page.locator('[data-summary="en-CA"]').getByRole("heading", { level: 2, name: "Description" })).toBeVisible()
	await expect(page.getByRole("heading", { level: 1 })).toHaveCount(1)
})

// --- The admin pages ---

Given(
	'a signed-in Safety Officer and a report whose summary has a "## Description" section in each language',
	async ({ page }) => {
		await stubReportDetail(page, reportDetail())
	},
)

When("they open that report", async ({ page }) => {
	await openReport(page)
})

Then('each language\'s summary shows a heading "Description" and its text as a paragraph', async ({ page }) => {
	for (const [language, text] of [
		["en", "The pilot launched in a gusting wind."],
		["fr", "Le pilote a décollé par vent en rafales."],
	]) {
		const summary = page.locator(`[data-summary="${language}"]`)
		await expect(summary.getByRole("heading", { name: "Description" })).toBeVisible()
		await expect(summary.locator("p").first()).toHaveText(text)
		// The markers themselves are never shown.
		await expect(summary).not.toContainText("##")
	}
})

Then("opening a version in the history shows its sections the same way", async ({ page }) => {
	await page.getByRole("button", { name: "View this version" }).last().click()

	const english = page.locator('[data-revision-text="en"]')
	await expect(english.getByRole("heading", { name: "Description" })).toBeVisible()
	await expect(english.locator("p").first()).toHaveText("An earlier version of the description.")
	await expect(english).not.toContainText("##")
})

Given(
	"a signed-in Safety Officer and a report with a long-text answer written in Markdown and its Worker translation",
	async ({ page }) => {
		await stubReportDetail(
			page,
			reportDetail([
				{
					questionKey: "narrative",
					labelEn: "Description",
					labelFr: "Description",
					type: "long_text",
					isPrivate: false,
					values: [
						{
							value: "A **firm** landing:\n\n- the wing collapsed\n- the reserve was thrown",
							locale: "en-CA",
							translatedValue: "Un atterrissage **ferme** :\n\n- l'aile s'est fermée\n- le parachute a été lancé",
							translationSource: "auto",
						},
					],
				},
				{
					questionKey: "wing_model",
					labelEn: "Model",
					labelFr: "Modèle",
					type: "short_text",
					isPrivate: false,
					values: [{ value: "Sky **Lite** 2", locale: "en-CA", translatedValue: null, translationSource: null }],
				},
			]),
		)
	},
)

Then("the answer shows its bold text as bold and its list as a list", async ({ page }) => {
	const answer = page.locator('[data-question-key="narrative"] [data-long-text-answer]')
	await expect(answer.locator("strong")).toHaveText("firm")
	await expect(answer.locator("ul > li")).toHaveCount(2)
	await expect(answer).not.toContainText("**")
})

Then("its translation shows the same formatting", async ({ page }) => {
	const translation = page.locator('[data-question-key="narrative"] [data-long-text-translation]')
	await expect(translation.locator("strong")).toHaveText("ferme")
	await expect(translation.locator("ul > li")).toHaveCount(2)
	await expect(translation).not.toContainText("**")
})

Then("a short-text answer with Markdown characters is shown as written", async ({ page }) => {
	const answer = page.locator('[data-question-key="wing_model"]')
	await expect(answer).toContainText("Sky **Lite** 2")
	await expect(answer.locator("strong, em")).toHaveCount(0)
})

// --- REQ-MOD-210: the feed's preview ---

Given(
	'the public feed has a report whose summary has a "## Description" section and a second section',
	async ({ page }) => {
		await page.route(/\/api\/v1\/public\/reports\/?(\?.*)?$/, (route) =>
			route.fulfill({
				json: {
					items: [
						{
							id: FEED_ID,
							aiSummaryEn: "## Description\nThe pilot launched in a **gusting** crosswind.\n\nThe lines were torn.\n\n## Action and prevention\nThe club was told.",
							aiSummaryFr: "## Description\nLe pilote a décollé par un vent de travers en **rafales**.\n\nLes suspentes étaient déchirées.\n\n## Action et prévention\nLe club a été informé.",
							publishedAt: "2026-09-20T15:30:00Z",
							commentCount: 0,
							attachmentCount: 0,
						},
					],
					next: null,
				},
			}),
		)
	},
)

Then("the report's preview shows the body of the first section as plain text", async ({ page }) => {
	const preview = page.locator(`[data-report-id="${FEED_ID}"]`)
	await expect(preview).toContainText("The pilot launched in a gusting crosswind.")
	await expect(preview).toContainText("The lines were torn.")
	await expect(preview).not.toContainText("The club was told.")
})

Then("it shows no heading and no Markdown characters", async ({ page }) => {
	const preview = page.locator(`[data-report-id="${FEED_ID}"]`)
	await expect(preview).not.toContainText("Description")
	await expect(preview).not.toContainText("Action and prevention")
	await expect(preview).not.toContainText("#")
	await expect(preview).not.toContainText("*")
	await expect(preview.getByRole("heading")).toHaveCount(0)
})

// --- REQ-MOD-211: nothing advertises Markdown ---

Given("a signed-in Safety Officer and a report whose summary is Markdown", async ({ page }) => {
	await stubReportDetail(page, reportDetail())
	await openReport(page)
})

When("they start editing the summary", async ({ page }) => {
	await page.getByRole("button", { name: "Edit summary" }).click()
})

Then("each language is a plain text area holding the Markdown as written", async ({ page }) => {
	const english = page.getByLabel("English summary")
	const french = page.getByLabel("French summary")

	expect(await english.evaluate((element) => element.tagName)).toBe("TEXTAREA")
	expect(await french.evaluate((element) => element.tagName)).toBe("TEXTAREA")
	await expect(english).toHaveValue(SUMMARY_EN)
	await expect(french).toHaveValue(SUMMARY_FR)
})

Then("no Markdown toolbar, preview, or hint appears", async ({ page }) => {
	await expect(page.getByRole("toolbar")).toHaveCount(0)
	await expect(page.getByText(/markdown/i)).toHaveCount(0)
})
