import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

import { CREDENTIALS, signInAs, stubAuth } from "./auth"

const { Given, When, Then } = createBdd()

/*
 * The @ui scenarios for comments on a published report (REQ-COM-015..020,
 * ADR-0114).
 *
 * The public API is stubbed at the network boundary with a small in-memory
 * store per page, so posting, editing, deleting, and hiding change what the
 * next read returns. Who may do each, the revisions, the audit rows, and the
 * translation job are proven against a real database by the Reqnroll
 * scenarios REQ-COM-001..014 (ADR-0045).
 *
 * Every report and comment below is synthetic.
 */

interface StubComment {
	id: string
	text: string
	locale: string
	translatedText: string | null
	createdAt: string
	updatedAt: string
	edited: boolean
	isMine: boolean
}

const REPORT = {
	id: "commentaaa1",
	aiSummaryEn: "The pilot landed in a field after a crosswind launch.",
	aiSummaryFr: "Le pilote s'est posé dans un champ après un décollage par vent de travers.",
	publishedAt: "2026-09-20T15:30:00Z",
}

function comment(id: string, text: string, overrides: Partial<StubComment> = {}): StubComment {
	return {
		id,
		text,
		locale: "en-CA",
		translatedText: null,
		createdAt: "2026-09-21T10:00:00Z",
		updatedAt: "2026-09-21T10:00:00Z",
		edited: false,
		isMine: false,
		...overrides,
	}
}

const stores = new WeakMap<Page, StubComment[]>()

async function stubReport(page: Page, comments: StubComment[]) {
	stores.set(page, comments)

	await page.route(/\/api\/v1\/public\/reports\/?(\?.*)?$/, async (route) => {
		await route.fulfill({
			json: {
				items: [
					{ ...REPORT, commentCount: comments.length },
					{ ...REPORT, id: "commentaaa2", commentCount: 1 },
					{ ...REPORT, id: "commentaaa3", commentCount: 0 },
				],
				next: null,
			},
		})
	})

	await page.route(/\/api\/v1\/public\/reports\/[^/?]+$/, async (route) => {
		await route.fulfill({ json: { ...REPORT, commentCount: comments.length, media: [] } })
	})

	await page.route(/\/api\/v1\/public\/reports\/[^/?]+\/comments\/?(\?.*)?$/, async (route) => {
		const request = route.request()
		if (request.method() === "POST") {
			const body = request.postDataJSON() as { text: string; locale: string }
			const posted = comment(`postedaaa${comments.length}`, body.text, { locale: body.locale, isMine: true })
			comments.push(posted)
			return route.fulfill({ status: 201, json: posted })
		}
		return route.fulfill({ json: comments })
	})

	await page.route(/\/api\/v1\/public\/reports\/[^/?]+\/comments\/[^/?]+$/, async (route) => {
		const request = route.request()
		const id = new URL(request.url()).pathname.split("/").pop()!
		const index = comments.findIndex((candidate) => candidate.id === id)
		if (request.method() === "DELETE") {
			comments.splice(index, 1)
			return route.fulfill({ status: 204, body: "" })
		}
		const body = request.postDataJSON() as { text: string; locale: string }
		comments[index] = { ...comments[index], text: body.text, locale: body.locale, translatedText: null, edited: true }
		return route.fulfill({ json: comments[index] })
	})

	await page.route(/\/api\/admin\/comments\/[^/?]+\/hide$/, async (route) => {
		const id = route.request().url().split("/").at(-2)
		comments.splice(
			comments.findIndex((candidate) => candidate.id === id),
			1,
		)
		await route.fulfill({ status: 204, body: "" })
	})
}

// By data attribute, not accessible name: the French scenarios read the page
// in French, where the list's label is translated.
function items(page: Page) {
	return page.locator("li[data-comment-id]")
}

const OTHERS = () => [
	comment("othersaaaa1", "Synthetic: a useful reminder about field selection."),
	comment("othersaaaa2", "Synthétique : bon rappel.", { locale: "fr-CA", translatedText: "Synthetic: good reminder." }),
]

// ── Given ───────────────────────────────────────────────────────────────

Given("the public feed has published reports with comments", async ({ page }) => {
	await stubReport(page, OTHERS())
})

Given("a published report has comments", async ({ page }) => {
	await stubReport(page, OTHERS())
})

Given("a member is signed in and a published report has comments", async ({ page }) => {
	await stubReport(page, OTHERS())
	await signInAs(page, "user")
})

Given("a safety officer is signed in and a published report has comments", async ({ page }) => {
	await stubReport(page, OTHERS())
	await signInAs(page, "safety_officer")
})

Given("a published report has a comment written in English and machine-translated into French", async ({ page }) => {
	await stubReport(page, [
		comment("translated1", "Synthetic: keep extra height on approach.", {
			translatedText: "Synthétique : gardez de la hauteur en approche.",
		}),
	])
})

Given("a published report has a comment written in English that has no French text yet", async ({ page }) => {
	await stubReport(page, [comment("untransl001", "Synthetic: keep extra height on approach.")])
})

// ── When ────────────────────────────────────────────────────────────────

When("a visitor opens View safety reports", async ({ page }) => {
	await page.goto("/reports")
})

When("a visitor who is not signed in opens it", async ({ page }) => {
	await page.goto(`/reports/${REPORT.id}`)
})

When("the member opens the report", async ({ page }) => {
	await page.goto(`/reports/${REPORT.id}`)
})

When("the safety officer opens the report", async ({ page }) => {
	await page.goto(`/reports/${REPORT.id}`)
})

When("a visitor reads the report in French", async ({ page, context }) => {
	await context.addInitScript(() => localStorage.setItem("hpac.locale", "fr-CA"))
	await page.goto(`/reports/${REPORT.id}`)
})

When("the visitor signs in from there", async ({ page }) => {
	await stubAuth(page)
	await page.getByRole("link", { name: "Sign in to comment" }).click()
	await page.getByLabel("Username").fill(CREDENTIALS.user.username)
	await page.getByLabel("Password").fill(CREDENTIALS.user.password)
	await page.getByRole("button", { name: "Log in" }).click()
})

When("the member posts a comment", async ({ page }) => {
	await page.getByLabel("Add a comment").fill("Synthetic: I made the same mistake last season.")
	await page.getByRole("button", { name: "Post comment" }).click()
})

When("the member edits that comment", async ({ page }) => {
	const mine = page.locator('[data-comment-author="you"]')
	await mine.getByRole("button", { name: "Edit" }).click()
	await mine.getByLabel("Edit your comment").fill("Synthetic, edited: the same mistake, two seasons ago.")
	await mine.getByRole("button", { name: "Save" }).click()
})

When("the member deletes that comment and confirms", async ({ page }) => {
	const mine = page.locator('[data-comment-author="you"]')
	await mine.getByRole("button", { name: "Delete" }).click()
	await mine.getByRole("button", { name: "Delete" }).click()
})


When("the safety officer hides a comment and confirms", async ({ page }) => {
	const first = items(page).first()
	await first.getByRole("button", { name: "Hide" }).click()
	await first.getByRole("button", { name: "Hide" }).click()
})

// ── Then ────────────────────────────────────────────────────────────────

Then("each report shows its number of comments", async ({ page }) => {
	await expect(page.locator(`[data-report-id="${REPORT.id}"]`)).toContainText("2 comments")
	await expect(page.locator('[data-report-id="commentaaa2"]')).toContainText("1 comment")
	await expect(page.locator('[data-report-id="commentaaa3"]')).toContainText("0 comments")
})

Then('the comments are shown, each labelled "Member"', async ({ page }) => {
	await expect(items(page)).toHaveCount(2)
	for (const item of await items(page).all()) {
		await expect(item).toContainText("Member")
	}
})

Then("instead of a comment box the page offers to sign in to comment", async ({ page }) => {
	await expect(page.getByRole("link", { name: "Sign in to comment" })).toBeVisible()
	await expect(page.getByLabel("Add a comment")).toHaveCount(0)
})

Then("the visitor is returned to the report", async ({ page }) => {
	await expect(page).toHaveURL(new RegExp(`/reports/${REPORT.id}$`))
	await expect(page.getByLabel("Add a comment")).toBeVisible()
})

Then("a comment box is shown with a reminder not to name or identify people", async ({ page }) => {
	await expect(page.getByLabel("Add a comment")).toBeVisible()
	await expect(page.getByText("Do not name or identify anyone")).toBeVisible()
})

Then('the comment is listed, labelled "You"', async ({ page }) => {
	const mine = page.locator('[data-comment-author="you"]')
	await expect(mine).toHaveCount(1)
	await expect(mine).toContainText("You")
	await expect(mine).toContainText("I made the same mistake last season.")
})

Then("the comment shows the new text, marked as edited", async ({ page }) => {
	const mine = page.locator('[data-comment-author="you"]')
	await expect(mine.locator("[data-comment-text]")).toHaveText("Synthetic, edited: the same mistake, two seasons ago.")
	await expect(mine.locator("[data-comment-edited]")).toBeVisible()
})

Then("the comment is no longer listed", async ({ page }) => {
	await expect(page.locator('[data-comment-author="you"]')).toHaveCount(0)
	await expect(items(page)).toHaveCount(2)
})

Then("no other member's comment offers to edit or delete it", async ({ page }) => {
	for (const item of await items(page).all()) {
		await expect(item.getByRole("button", { name: /^(Edit|Delete)$/ })).toHaveCount(0)
	}
})



Then("the comment shows its English text, marked as awaiting translation", async ({ page }) => {
	const first = items(page).first()
	await expect(first.locator("[data-comment-text]")).toHaveText("Synthetic: keep extra height on approach.")
	await expect(first.locator("[data-comment-awaiting]")).toBeVisible()
})

Then("every comment offers to hide it", async ({ page }) => {
	for (const item of await items(page).all()) {
		await expect(item.getByRole("button", { name: "Hide" })).toBeVisible()
	}
})

Then("that comment is no longer listed", async ({ page }) => {
	await expect(items(page)).toHaveCount(1)
	await expect(page.locator('[data-comment-id="othersaaaa1"]')).toHaveCount(0)
})

Then("the comment shows its French text, with a small icon that says it was translated automatically", async ({ page }) => {
	const first = items(page).first()
	await expect(first.locator("[data-comment-text]")).toHaveText("Synthétique : gardez de la hauteur en approche.")
	await expect(first.locator("[data-comment-text]")).toHaveAttribute("lang", "fr-CA")
	const icon = first.locator("[data-comment-translated]")
	await expect(icon).toBeVisible()
	await expect(icon).toHaveAttribute("title", /.+/)
	await expect(icon.locator(".sr-only")).not.toBeEmpty()
})

Then("the comment offers no control to show the original", async ({ page }) => {
	await expect(items(page).first().getByRole("button")).toHaveCount(0)
})

Then("the comment shows its English text, with no translation icon", async ({ page }) => {
	const first = items(page).first()
	await expect(first.locator("[data-comment-text]")).toHaveText("Synthetic: keep extra height on approach.")
	await expect(first.locator("[data-comment-text]")).toHaveAttribute("lang", "en-CA")
	await expect(first.locator("[data-comment-translated]")).toHaveCount(0)
})
