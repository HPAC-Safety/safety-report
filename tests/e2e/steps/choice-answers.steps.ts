import { createBdd } from "playwright-bdd"
import { expect, type Page, type Request } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import { stubCurrentQuestions, stubSubmission, type StubQuestion } from "./report-form-fixture"

const { Given, When, Then } = createBdd()

/*
 * REQ-SUB-083: the form names each chosen choice by its identifier (ADR-0128),
 * and sends a type-ahead value no choice carries yet as the words typed
 * (ADR-0129). The API is stubbed at the network boundary; what the server does
 * with each is REQ-SUB-078 and REQ-SUB-079.
 */

function choiceForm(): StubQuestion[] {
	const base = {
		role: "none",
		isRequired: false,
		isPrivate: false,
		dependsOnQuestionId: null,
		dependsOnChoiceId: null,
		allowsReporterAdditions: false,
		helpTextEn: null,
		helpTextFr: null,
		placeholderEn: null,
		placeholderFr: null,
		children: [],
	}

	return [
		{
			...base,
			id: "wing",
			revisionId: "rev-wing",
			key: "wing_type",
			type: "single_select",
			labelEn: "Type of wing",
			labelFr: "Type d'aile",
			displayOrder: 0,
			options: [
				{ id: "choice-paraglider", code: "paraglider", labelEn: "Paraglider", labelFr: "Parapente", onlyIn: null },
				{ id: "choice-hang_glider", code: "hang_glider", labelEn: "Hang glider", labelFr: "Deltaplane", onlyIn: null },
			],
		},
		{
			...base,
			id: "conditions",
			revisionId: "rev-conditions",
			key: "conditions",
			type: "multi_select",
			labelEn: "Which conditions applied?",
			labelFr: "Quelles conditions?",
			displayOrder: 1,
			options: [
				{ id: "choice-gusty", code: "gusty", labelEn: "Gusty", labelFr: "Rafales", onlyIn: null },
				{ id: "choice-thermic", code: "thermic", labelEn: "Thermic", labelFr: "Thermique", onlyIn: null },
				{ id: "choice-turbulent", code: "turbulent", labelEn: "Turbulent", labelFr: "Turbulent", onlyIn: null },
			],
		},
		{
			...base,
			id: "launch",
			revisionId: "rev-launch",
			key: "launch_site",
			type: "autocomplete",
			labelEn: "Where did you launch?",
			labelFr: "D'où avez-vous décollé?",
			displayOrder: 2,
			allowsReporterAdditions: true,
			options: [{ id: "choice-mount_7", code: "mount_7", labelEn: "Mount 7", labelFr: "Mont 7", onlyIn: null }],
		},
		{
			...base,
			id: "consent",
			revisionId: "rev-consent",
			key: "consent_publish",
			role: "consent_publish",
			type: "yes_no",
			labelEn: "May we publish a summary of this report?",
			labelFr: "Pouvons-nous publier un résumé de ce signalement?",
			displayOrder: 3,
			isRequired: true,
			options: [],
		},
	] as StubQuestion[]
}

const sent = new WeakMap<Page, Request>()

Given(
	"a signed-in reporter picks a wing type, checks two conditions, and types a launch site the form does not offer",
	async ({ page }) => {
		await stubAuth(page)
		await stubCurrentQuestions(page, choiceForm())
		await stubSubmission(page)
		await signInAs(page, "user")
		await page.goto("/report")

		await page.getByLabel("Type of wing").selectOption({ label: "Hang glider" })
		await page.getByRole("button", { name: "Next" }).click()

		await page.getByRole("button", { name: /Which conditions applied\?/ }).click()
		await page.getByRole("checkbox", { name: "Gusty" }).check()
		await page.getByRole("checkbox", { name: "Turbulent" }).check()
		await page.keyboard.press("Escape") // closes the picker over the page's buttons
		await page.getByRole("button", { name: "Next" }).click()

		await page.getByRole("combobox", { name: "Where did you launch?" }).fill("A ridge nobody listed")
		await page.getByRole("button", { name: "Next" }).click()

		await page.getByRole("radio", { name: "Yes" }).click()
	},
)

When("the reporter sends the report", async ({ page }) => {
	const request = page.waitForRequest((candidate) => candidate.url().includes("/api/v1/reports/") && candidate.method() === "POST")
	await page.getByRole("button", { name: "Submit report" }).click()
	sent.set(page, await request)
})

type SentAnswer = { questionRevisionId: string; value: string | null; choices: string[] | null }

function answerTo(page: Page, revisionId: string): SentAnswer | undefined {
	const body = sent.get(page)!.postDataJSON() as { answers: SentAnswer[] }
	return body.answers.find((answer) => answer.questionRevisionId === revisionId)
}

Then("the wing type and both conditions are sent as their choices' identifiers", async ({ page }) => {
	expect(answerTo(page, "rev-wing")).toMatchObject({ value: null, choices: ["choice-hang_glider"] })
	expect(answerTo(page, "rev-conditions")?.value).toBeNull()
	expect([...(answerTo(page, "rev-conditions")?.choices ?? [])].sort()).toEqual(["choice-gusty", "choice-turbulent"])
})

Then("the launch site is sent as the words typed", async ({ page }) => {
	expect(answerTo(page, "rev-launch")).toMatchObject({ value: "A ridge nobody listed", choices: null })
})

/*
 * REQ-QB-171: a type-ahead choice picked from the list is sent as that choice,
 * by its identifier, even where another choice carries the same wording. The
 * stub sends north first, so matching the wording would name north; the list
 * sorts the tie by ID, north then south, and the reporter picks south.
 */

function sameWordingForm(wording: string): StubQuestion[] {
	const [, , launch, consent] = choiceForm()
	return [
		{
			...launch,
			displayOrder: 0,
			options: [
				{ id: "choice-other-north", code: "other_north", labelEn: wording, labelFr: wording, onlyIn: null },
				{ id: "choice-other-south", code: "other_south", labelEn: wording, labelFr: wording, onlyIn: null },
			],
		},
		{ ...consent, displayOrder: 1 },
	]
}

Given("a signed-in reporter answers a type-ahead question offering two choices both worded {string}", async ({ page }, wording: string) => {
	await stubAuth(page)
	await stubCurrentQuestions(page, sameWordingForm(wording))
	await stubSubmission(page)
	await signInAs(page, "user")
	await page.goto("/report")
})

When("they pick the second {string} from the list", async ({ page }, wording: string) => {
	await page.getByRole("button", { name: "Show choices" }).click()
	await page.getByRole("listbox").getByRole("option", { name: wording }).nth(1).click()
})

When("they consent on the next page and send the report", async ({ page }) => {
	await page.getByRole("button", { name: "Next" }).click()
	await page.getByRole("radio", { name: "Yes" }).click()

	const request = page.waitForRequest((candidate) => candidate.url().includes("/api/v1/reports/") && candidate.method() === "POST")
	await page.getByRole("button", { name: "Submit report" }).click()
	sent.set(page, await request)
})

Then("the answer names the second {string} choice's identifier and carries no typed text", async ({ page }, _wording: string) => {
	expect(answerTo(page, "rev-launch")).toMatchObject({ value: null, choices: ["choice-other-south"] })
})
