import { describe, expect, it } from "vitest"
import type { ReportListItem } from "../api/adminReports"
import { rowActionsFor } from "./rowActions"

const report = (overrides: Partial<ReportListItem>): ReportListItem => ({
	id: "r",
	submittedAt: "2026-01-01T00:00:00Z",
	status: "pending",
	language: "en-CA",
	consent: true,
	isStuck: false,
	version: "v",
	reporterName: null,
	pilotName: null,
	attachmentCount: 0,
	...overrides,
})

describe("rowActionsFor", () => {
	it("offers Publish and Delete on a pending report", () => {
		expect(rowActionsFor(report({ status: "pending" }))).toEqual(["publish", "delete"])
	})

	it("offers Unpublish and Delete on a published report", () => {
		expect(rowActionsFor(report({ status: "published" }))).toEqual(["unpublish", "delete"])
	})

	it("offers only Delete where the report view offers no publishing", () => {
		expect(rowActionsFor(report({ status: "submitted" }))).toEqual(["delete"])
		expect(rowActionsFor(report({ status: "pending", consent: false }))).toEqual(["delete"])
	})
})
