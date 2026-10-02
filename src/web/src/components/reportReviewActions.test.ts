import { describe, expect, it } from "vitest"
import { actionsFor } from "./reportReviewActions"

describe("actionsFor", () => {
	it("offers each state of a consented report its own actions", () => {
		expect(actionsFor({ status: "pending", consent: true })).toEqual(["edit", "publish", "unpublish", "delete"])
		expect(actionsFor({ status: "published", consent: true })).toEqual(["edit", "unpublish", "delete"])
		expect(actionsFor({ status: "unpublished", consent: true })).toEqual(["edit", "publish", "delete"])
		expect(actionsFor({ status: "summary_failed", consent: true })).toEqual(["write", "delete"])
		expect(actionsFor({ status: "submitted", consent: true })).toEqual(["delete"])
		expect(actionsFor({ status: "summarizing", consent: true })).toEqual(["delete"])
	})

	it("offers only deletion without consent, whatever the state", () => {
		expect(actionsFor({ status: "pending", consent: false })).toEqual(["delete"])
		expect(actionsFor({ status: "published", consent: null })).toEqual(["delete"])
	})
})
