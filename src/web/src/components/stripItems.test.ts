import { beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "../api/adminQuestions"
import { attachmentLink, type ReportAttachment } from "../api/adminReports"
import { fetchMediaLink, PublicReportNotFound } from "../api/publicReports"
import { isGone, itemsFromPublicMedia, itemsFromStaffAttachments, linkFor, type StripItem } from "./stripItems"

vi.mock("../api/adminReports", () => ({ attachmentLink: vi.fn() }))
vi.mock("../api/publicReports", async (importOriginal) => ({
	...(await importOriginal<typeof import("../api/publicReports")>()),
	fetchMediaLink: vi.fn(),
}))

const link = { url: "https://files/x", expiresAt: "2030-01-01T00:00:00Z", fileName: "x.png" }

beforeEach(() => {
	vi.mocked(attachmentLink).mockReset().mockResolvedValue(link)
	vi.mocked(fetchMediaLink).mockReset().mockResolvedValue(link)
})

describe("isGone", () => {
	it("is true for a report that is no longer public and for a 404", () => {
		expect(isGone(new PublicReportNotFound())).toBe(true)
		expect(isGone(new ApiError(404, "gone"))).toBe(true)
	})

	it("is false for any other failure", () => {
		expect(isGone(new ApiError(500, "boom"))).toBe(false)
		expect(isGone(new Error("network"))).toBe(false)
		expect(isGone(null)).toBe(false)
	})
})

describe("the strip item shapes", () => {
	it("reads a public media list as ready items with no visibility", () => {
		expect(itemsFromPublicMedia([{ id: "a", kind: "image", format: null }])).toEqual([
			{ id: "a", kind: "image", format: null, state: "ready", visibility: null },
		])
	})

	it("reads staff attachments with their state and visibility", () => {
		const attachment: ReportAttachment = { id: "b", kind: "document", state: "processing", visibility: "hidden", format: "pdf" }
		expect(itemsFromStaffAttachments([attachment])).toEqual([attachment])
	})
})

describe("linkFor", () => {
	const item = (over: Partial<StripItem>): StripItem => ({ id: "i", kind: "image", format: null, state: "ready", visibility: "private", ...over })

	it("uses the public link for a public viewer", async () => {
		await expect(linkFor("r", item({ visibility: null }), false)).resolves.toBe(link)
		expect(fetchMediaLink).toHaveBeenCalledWith("r", "i")
	})

	it("uses the public link for a public item even when staff look", async () => {
		await linkFor("r", item({ visibility: "public" }), true)
		expect(fetchMediaLink).toHaveBeenCalledWith("r", "i")
		expect(attachmentLink).not.toHaveBeenCalled()
	})

	it("uses the audited staff mint for a non-public item", async () => {
		await linkFor("r", item({ kind: "video", state: "ready", visibility: "hidden", format: "mp4" }), true)
		expect(attachmentLink).toHaveBeenCalledWith("r", { id: "i", kind: "video", state: "ready", visibility: "hidden", format: "mp4" })
	})

	it("treats a staff item with no visibility as private", async () => {
		await linkFor("r", item({ visibility: null }), true)
		expect(attachmentLink).toHaveBeenCalledWith("r", expect.objectContaining({ visibility: "private" }))
	})
})
