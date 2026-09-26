import { createBdd } from "playwright-bdd"
import { expect, type Page } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"

const { Given, When, Then } = createBdd()

/*
 * REQ-MOD-095 and REQ-MOD-097: a Safety Officer reviews reporter-added type-ahead values on
 * one page (ADR-0129). The API is stubbed at the network boundary: what each
 * review does to the value, and who may do it, is REQ-QB-128..135 and the
 * API tests. Every value here is synthetic.
 */

interface StubValue {
	id: string
	questionId: string
	questionLabelEn: string
	questionLabelFr: string
	labelEn: string | null
	labelFr: string | null
	typedIn: string | null
	isRemoved: boolean
	answerCount: number
	addedAt: string | null
	mergeTargets: { id: string; labelEn: string | null; labelFr: string | null }[]
}

const WHERE = { questionId: "q-where", questionLabelEn: "Where did this happen?", questionLabelFr: "Où cela s'est-il produit ?" }

const LAUNCH = { questionId: "q-launch", questionLabelEn: "Where did you launch?", questionLabelFr: "D'où avez-vous décollé ?" }

function flaggedValues(): StubValue[] {
	return [
		{ ...WHERE, id: "value-mount7", labelEn: "Mount 7", labelFr: null, typedIn: "en-CA", isRemoved: false, answerCount: 2, addedAt: "2026-09-20T12:00:00Z", mergeTargets: [] },
		{ ...WHERE, id: "value-coopers", labelEn: "coopers", labelFr: null, typedIn: "en-CA", isRemoved: false, answerCount: 1, addedAt: "2026-09-21T12:00:00Z", mergeTargets: [] },
		{ ...WHERE, id: "value-test", labelEn: "Test site", labelFr: null, typedIn: "en-CA", isRemoved: false, answerCount: 3, addedAt: "2026-09-22T12:00:00Z", mergeTargets: [] },
	]
}

function duplicateValues(): StubValue[] {
	const coopersHill = { id: "value-coopers-apostrophe", labelEn: "Cooper's", labelFr: null }
	const coopers = { id: "value-coopers-plain", labelEn: "Coopers", labelFr: null }
	return [
		{ ...WHERE, ...coopers, typedIn: "en-CA", isRemoved: false, answerCount: 2, addedAt: "2026-09-20T12:00:00Z", mergeTargets: [coopersHill] },
		{ ...WHERE, ...coopersHill, typedIn: "en-CA", isRemoved: false, answerCount: 5, addedAt: "2026-09-21T12:00:00Z", mergeTargets: [coopers] },
		{ ...LAUNCH, id: "value-sainte-anne", labelEn: null, labelFr: "Élévation Sainte-Anne", typedIn: "fr-CA", isRemoved: false, answerCount: 1, addedAt: "2026-09-22T12:00:00Z", mergeTargets: [] },
	]
}

type Review = { method: string; id: string; body: unknown }

const reviews = new WeakMap<Page, Review[]>()

Given("a signed-in Safety Officer and two type-ahead questions with values flagged for review", async ({ page }) => {
	await reviewPage(page, duplicateValues())
})

When('they merge "Coopers" into "Cooper\'s"', async ({ page }) => {
	const coopers = valueRow(page, "Coopers")
	await coopers.getByLabel("Merge into…").selectOption({ label: "Cooper's" })
	await coopers.getByRole("button", { name: "Merge" }).click()
})

Then('"Coopers" leaves the list', async ({ page }) => {
	await expect(valueRow(page, "Coopers")).toHaveCount(0)
	expect(reviews.get(page)).toEqual([{ method: "POST", id: "value-coopers-plain", body: { intoId: "value-coopers-apostrophe" } }])
})

Then('"Cooper\'s" is no longer flagged', async ({ page }) => {
	await expect(valueRow(page, "Cooper's")).toHaveCount(0)
	await expect(valueRow(page, "Élévation Sainte-Anne")).toHaveCount(1)
})

Then("every flagged value is listed with its question, its language, and how many answers name it", async ({ page }) => {
	await expect(page.getByTestId("type-ahead-value")).toHaveCount(3)
	const sainteAnne = valueRow(page, "Élévation Sainte-Anne")
	await expect(sainteAnne).toContainText("Where did you launch?")
	await expect(sainteAnne).toContainText("Typed in fr-CA")
	await expect(valueRow(page, "Cooper's")).toContainText("Answers naming it: 5")
})

Given("a signed-in Safety Officer and three type-ahead values flagged for review", async ({ page }) => {
	await reviewPage(page, flaggedValues())
})

/**
 * Stubs the review API with these values. A review removes the value it names
 * from the list; a merge removes its target too, which it reviews as well
 * (ADR-0129).
 */
async function reviewPage(page: Page, values: StubValue[]) {
	const waiting = values
	const sent: Review[] = []
	reviews.set(page, sent)

	await stubAuth(page)
	await page.route("**/api/admin/type-ahead-values/**", async (route) => {
		const request = route.request()
		const url = new URL(request.url())

		if (url.pathname.endsWith("/awaiting-review")) {
			return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ values: waiting, count: waiting.length }) })
		}

		const id = decodeURIComponent(url.pathname.split("/")[4] ?? "")
		const body = request.postDataJSON() as { intoId?: string } | null
		sent.push({ method: request.method(), id, body })
		for (const reviewed of [id, url.pathname.endsWith("/merge") ? body?.intoId : undefined]) {
			const index = waiting.findIndex((value) => value.id === reviewed)
			if (index >= 0) waiting.splice(index, 1)
		}
		return route.fulfill({ status: 204 })
	})
	await signInAs(page, "safety_officer")
}

When("they open the review-type-ahead-values page", async ({ page }) => {
	await page.goto("/admin/type-ahead-values")
	await expect(page.getByRole("heading", { level: 1, name: "Type-ahead values to review" })).toBeVisible()
})

const valueRow = (page: Page, wording: string) =>
	page.getByTestId("type-ahead-value").filter({ has: page.getByTestId("type-ahead-value-wording").getByText(wording, { exact: true }) })

Then("each value is listed with its question, the language it was typed in, and how many answers name it", async ({ page }) => {
	await expect(page.getByTestId("type-ahead-value")).toHaveCount(3)
	const mount7 = valueRow(page, "Mount 7")
	await expect(mount7).toContainText("Where did this happen?")
	await expect(mount7).toContainText("Typed in en-CA")
	await expect(mount7).toContainText("Answers naming it: 2")
})

When('they approve "Mount 7", correct "coopers" to "Cooper\'s", and remove "Test site"', async ({ page }) => {
	await valueRow(page, "Mount 7").getByRole("button", { name: "Approve" }).click()
	await expect(page.getByTestId("type-ahead-value")).toHaveCount(2)

	await valueRow(page, "coopers").getByRole("button", { name: "Correct" }).click()
	await page.getByLabel("English wording").fill("Cooper's")
	await page.getByRole("button", { name: "Save correction" }).click()
	await expect(page.getByTestId("type-ahead-value")).toHaveCount(1)

	await valueRow(page, "Test site").getByRole("button", { name: "Remove" }).click()
})

Then("the API is asked to approve, correct, and remove exactly those values", async ({ page }) => {
	await expect.poll(() => reviews.get(page)!.length).toBe(3)
	const [approve, correct, remove] = reviews.get(page)!

	expect(approve).toMatchObject({ method: "POST", id: "value-mount7" })
	expect(correct).toMatchObject({ method: "PUT", id: "value-coopers", body: { labelEn: "Cooper's", labelFr: "" } })
	expect(remove).toMatchObject({ method: "DELETE", id: "value-test" })
})

Then("the page lists no value left to review", async ({ page }) => {
	await expect(page.getByText("No type-ahead values are waiting for review.")).toBeVisible()
})
