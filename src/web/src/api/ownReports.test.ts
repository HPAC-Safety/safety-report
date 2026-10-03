import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { PublicReportNotFound } from "./publicReports"
import { dropReceipts, fetchOwnMediaLink, fetchOwnReport, fetchOwnReports, ownSummaryIn, readReceipts, receiptFor, saveReceipt } from "./ownReports"

const fetchMock = vi.fn()

function reply(status: number, body?: unknown): Response {
	return {
		status,
		ok: status >= 200 && status < 300,
		json: () => (body === undefined ? Promise.reject(new Error("no body")) : Promise.resolve(body)),
	} as unknown as Response
}

describe("own reports api", () => {
	beforeEach(() => {
		fetchMock.mockReset()
		vi.stubGlobal("fetch", fetchMock)
		localStorage.clear()
	})
	afterEach(() => vi.unstubAllGlobals())

	describe("receipts", () => {
		it("keeps the report ID and the receipt and nothing else", () => {
			saveReceipt("r1", "rec1")

			expect(readReceipts()).toEqual([{ reportId: "r1", receipt: "rec1" }])
			expect(receiptFor("r1")).toBe("rec1")
			expect(receiptFor("r2")).toBeNull()
		})

		it("replaces the receipt of a report it already holds", () => {
			saveReceipt("r1", "old")
			saveReceipt("r1", "new")

			expect(readReceipts()).toEqual([{ reportId: "r1", receipt: "new" }])
		})

		it("keeps only the newest fifty", () => {
			for (let index = 0; index < 55; index++) saveReceipt(`r${index}`, `rec${index}`)

			expect(readReceipts()).toHaveLength(50)
			expect(receiptFor("r0")).toBeNull()
			expect(receiptFor("r54")).toBe("rec54")
		})

		it("forgets the ones it is told to drop, and removes the entry when none is left", () => {
			saveReceipt("r1", "rec1")
			saveReceipt("r2", "rec2")

			dropReceipts(["r1"])
			expect(readReceipts()).toEqual([{ reportId: "r2", receipt: "rec2" }])

			dropReceipts(["r2"])
			expect(localStorage.getItem("hpac.report.receipts")).toBeNull()
		})

		it("reads nothing from a damaged entry", () => {
			localStorage.setItem("hpac.report.receipts", "{not json")
			expect(readReceipts()).toEqual([])

			localStorage.setItem("hpac.report.receipts", JSON.stringify([{ reportId: 1 }, "x", { reportId: "r1", receipt: "rec1" }]))
			expect(readReceipts()).toEqual([{ reportId: "r1", receipt: "rec1" }])

			localStorage.setItem("hpac.report.receipts", JSON.stringify({ not: "a list" }))
			expect(readReceipts()).toEqual([])
		})

		it("still works when storage refuses", () => {
			vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
				throw new Error("blocked")
			})
			vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
				throw new Error("blocked")
			})

			expect(readReceipts()).toEqual([])
			expect(() => saveReceipt("r1", "rec1")).not.toThrow()
			expect(() => dropReceipts(["r1"])).not.toThrow()
			vi.restoreAllMocks()
		})
	})

	describe("fetchOwnReports", () => {
		it("sends nothing when the browser holds no receipt", async () => {
			expect(await fetchOwnReports()).toEqual([])
			expect(fetchMock).not.toHaveBeenCalled()
		})

		it("posts the receipts in the body, never in the address, and drops the settled ones", async () => {
			saveReceipt("r1", "rec1")
			saveReceipt("r2", "rec2")
			const item = { id: "r1", submittedAt: "2026-01-02T00:00:00Z", forPublication: true, aiSummaryEn: null, aiSummaryFr: null, attachmentCount: 0 }
			fetchMock.mockResolvedValue(reply(200, { items: [item], settled: ["r2"] }))

			expect(await fetchOwnReports()).toEqual([item])

			const [address, init] = fetchMock.mock.calls[0] as [string, RequestInit]
			expect(address).toBe("/api/v1/public/reports/own")
			expect(address).not.toContain("rec1")
			expect(init.method).toBe("POST")
			expect(JSON.parse(init.body as string)).toEqual({
				receipts: [
					{ reportId: "r1", receipt: "rec1" },
					{ reportId: "r2", receipt: "rec2" },
				],
			})
			expect(readReceipts()).toEqual([{ reportId: "r1", receipt: "rec1" }])
		})

		it("shows none, and keeps every receipt, when the lookup fails", async () => {
			saveReceipt("r1", "rec1")

			fetchMock.mockResolvedValue(reply(500))
			expect(await fetchOwnReports()).toEqual([])

			fetchMock.mockRejectedValue(new Error("offline"))
			expect(await fetchOwnReports()).toEqual([])

			expect(readReceipts()).toHaveLength(1)
		})
	})

	describe("fetchOwnReport", () => {
		it("is null without a receipt, and asks for nothing", async () => {
			expect(await fetchOwnReport("r1")).toBeNull()
			expect(fetchMock).not.toHaveBeenCalled()
		})

		it("posts the receipt in the body", async () => {
			saveReceipt("r1", "rec1")
			const detail = { id: "r1", language: "en-CA", media: [] }
			fetchMock.mockResolvedValue(reply(200, detail))

			expect(await fetchOwnReport("r1")).toEqual(detail)

			const [address, init] = fetchMock.mock.calls[0] as [string, RequestInit]
			expect(address).toBe("/api/v1/public/reports/own/r1")
			expect(JSON.parse(init.body as string)).toEqual({ receipt: "rec1" })
		})

		it("drops the receipt and answers null on 404", async () => {
			saveReceipt("r1", "rec1")
			fetchMock.mockResolvedValue(reply(404))

			expect(await fetchOwnReport("r1")).toBeNull()
			expect(receiptFor("r1")).toBeNull()
		})

		it("fails, keeping the receipt, on any other error", async () => {
			saveReceipt("r1", "rec1")
			fetchMock.mockResolvedValue(reply(500))

			await expect(fetchOwnReport("r1")).rejects.toThrow("500")
			expect(receiptFor("r1")).toBe("rec1")
		})
	})

	describe("fetchOwnMediaLink", () => {
		it("posts the receipt in the body and returns the link", async () => {
			fetchMock.mockResolvedValue(reply(200, { url: "https://files/x", expiresAt: "2026-01-02T00:15:00Z" }))

			expect(await fetchOwnMediaLink("r1", "m1", "rec1")).toEqual({ url: "https://files/x", expiresAt: "2026-01-02T00:15:00Z" })

			const [address, init] = fetchMock.mock.calls[0] as [string, RequestInit]
			expect(address).toBe("/api/v1/public/reports/own/r1/media/m1")
			expect(JSON.parse(init.body as string)).toEqual({ receipt: "rec1" })
		})

		it("says the file is gone on 404", async () => {
			fetchMock.mockResolvedValue(reply(404))
			await expect(fetchOwnMediaLink("r1", "m1", "rec1")).rejects.toBeInstanceOf(PublicReportNotFound)
		})

		it("fails on any other error", async () => {
			fetchMock.mockResolvedValue(reply(500))
			await expect(fetchOwnMediaLink("r1", "m1", "rec1")).rejects.toThrow("500")
		})
	})

	it("reads the summary in the site's language, or null before there is one", () => {
		const report = { id: "r1", submittedAt: "", forPublication: true, aiSummaryEn: "En", aiSummaryFr: "Fr", attachmentCount: 0 }

		expect(ownSummaryIn(report, "en-CA")).toBe("En")
		expect(ownSummaryIn(report, "fr-CA")).toBe("Fr")
		expect(ownSummaryIn({ ...report, aiSummaryEn: null, aiSummaryFr: null }, "en-CA")).toBeNull()
	})
})
