import { createBdd } from "playwright-bdd"
import { expect } from "@playwright/test"

import { signInAs, stubAuth } from "./auth"
import { stubCurrentQuestions } from "./report-form-fixture"

const { Given, Then } = createBdd()

// The feature's Background states what the submission endpoint accepts. Those
// are server-authoritative facts, asserted by HpacSafety.Api.Tests and by the
// untagged scenarios in this same file — but playwright-bdd needs a definition
// for every step it sees, including a Background a @ui scenario inherits.
//
// The slashes are escaped because a Cucumber Expression reads `/` as
// alternation, and an empty alternative is an error.
Given(
	"a reporter writes a report through POST \\/api\\/v1\\/reports and an attachment through POST \\/api\\/v1\\/uploads",
	async () => {},
)

Given("both require a valid member bearer token", async () => {})

Given("the report request is JSON that names each attachment by the upload ID the upload returned", async () => {})

Given("the bearer token is transport\\/security metadata, not persisted report content", async () => {})

Given("a signed-out visitor opens the report page", async ({ page }) => {
	await stubAuth(page)
	await page.goto("/report")
})

Given("a signed-in member opens the report page", async ({ page }) => {
	// Any role may file a report: submission is a membership capability, not a
	// privileged one (ADR-0067). `User` is the weakest, so it is the honest
	// one to assert with.
	await stubCurrentQuestions(page)
	await signInAs(page, "user")
	await page.goto("/report")
})

Given("a signed-in member opens the report page in French", async ({ page }) => {
	await stubCurrentQuestions(page)
	await signInAs(page, "user")
	await page.goto("/report")
	await page.getByRole("button", { name: "Français" }).click()
})

Then("the report page content is not shown", async ({ page }) => {
	await expect(page.getByRole("heading", { name: "Sign in to file a report" })).toBeVisible()
	// By role, not by text: the sign-in copy legitimately contains the same
	// words, so a substring match here would pass while the notice was visible.
	await expect(page.getByRole("heading", { name: "Your report is not linked to you" })).toBeHidden()
})

Then("the page explains that filing a report requires an HPAC member sign-in", async ({ page }) => {
	await expect(page.getByText(/requires an HPAC member sign-in/i)).toBeVisible()
})

Then("it offers a sign-in action", async ({ page }) => {
	await expect(page.getByRole("link", { name: "Sign in" })).toBeVisible()
})

Then("the report page content is shown", async ({ page }) => {
	await expect(page.getByRole("heading", { name: "Sign in to file a report" })).toBeHidden()
	await expect(page.locator("main h1")).toBeVisible()
})

Then("a notice states that signing in only confirms HPAC membership", async ({ page }) => {
	await expect(page.getByText(/only confirms that you are an HPAC member/i)).toBeVisible()
})

Then("the notice states that the report is not linked to their account", async ({ page }) => {
	await expect(page.getByRole("heading", { name: "Your report is not linked to you" })).toBeVisible()

	// The promise is specific: nothing about who filed it is recorded.
	await expect(page.getByText(/do not record your name, your membership, or any trace/i)).toBeVisible()
})

Then("the notice is shown in French", async ({ page }) => {
	await expect(page.locator("html")).toHaveAttribute("lang", "fr-CA")

	// The notice is still rendered, and its copy comes from the catalogue
	// rather than the component. Asserting the text is *not* the English is
	// deliberately left out: that is what tools/check-locales.mjs and the
	// `#`-stub gate enforce (ADR-0054), and duplicating it here would fail on
	// any branch whose French has not been translated yet.
	await expect(page.getByRole("heading", { level: 2 })).toBeVisible()
	await expect(page.getByRole("heading", { level: 2 })).not.toHaveText("")
})
