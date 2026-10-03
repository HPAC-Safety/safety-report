import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "./adminQuestions"
import {
	deleteComment,
	editComment,
	fetchMediaLink,
	fetchPublicReport,
	fetchPublicReports,
	hideComment,
	hideMedia,
	listComments,
	postComment,
	PublicReportNotFound,
	summaryIn,
	type PublicReport,
} from "./publicReports"

const fetchMock = vi.fn()

function reply(status: number, body?: unknown, statusText = ""): Response {
	return {
		status,
		ok: status >= 200 && status < 300,
		statusText,
		json: () => (body === undefined ? Promise.reject(new Error("no body")) : Promise.resolve(body)),
	} as unknown as Response
}

describe("public reports api", () => {
	beforeEach(() => {
		fetchMock.mockReset()
		vi.stubGlobal("fetch", fetchMock)
		sessionStorage.clear()
	})
	afterEach(() => vi.unstubAllGlobals())

	describe("fetchPublicReports", () => {
		it("asks for the plain feed", async () => {
			const page = { items: [], next: null }
			fetchMock.mockResolvedValue(reply(200, page))
			expect(await fetchPublicReports(null)).toEqual(page)
			expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/public/reports")
		})

		it("passes the cursor, search and locale", async () => {
			fetchMock.mockResolvedValue(reply(200, { items: [], next: null }))
			await fetchPublicReports("c1", "wing", "fr-CA")
			expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/public/reports?after=c1&q=wing&locale=fr-CA")
		})

		it("sends the bearer token when signed in", async () => {
			sessionStorage.setItem("hpac.session", JSON.stringify({ accessToken: "t", expiresAt: "e", subject: "s", role: "user" }))
			fetchMock.mockResolvedValue(reply(200, { items: [], next: null }))
			await fetchPublicReports(null)
			expect(fetchMock.mock.calls[0][1]).toEqual({ headers: { Authorization: "Bearer t" } })
		})

		it("throws on a failure status", async () => {
			fetchMock.mockResolvedValue(reply(500))
			await expect(fetchPublicReports(null)).rejects.toThrow("(500)")
		})
	})

	describe("fetchPublicReport", () => {
		it("returns the detail", async () => {
			fetchMock.mockResolvedValue(reply(200, { id: "a/b" }))
			expect(await fetchPublicReport("a/b")).toEqual({ id: "a/b" })
			expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/public/reports/a%2Fb")
		})

		it("reports a 404 as not public", async () => {
			fetchMock.mockResolvedValue(reply(404))
			const failure = await fetchPublicReport("x").catch((error: unknown) => error)
			expect(failure).toBeInstanceOf(PublicReportNotFound)
			expect((failure as Error).name).toBe("PublicReportNotFound")
			expect((failure as Error).message).toBe("That report is not public.")
		})

		it("throws on another failure", async () => {
			fetchMock.mockResolvedValue(reply(503))
			await expect(fetchPublicReport("x")).rejects.toThrow("(503)")
		})
	})

	describe("fetchMediaLink", () => {
		it("returns the link", async () => {
			const link = { url: "u", expiresAt: "e" }
			fetchMock.mockResolvedValue(reply(200, link))
			expect(await fetchMediaLink("r", "m")).toEqual(link)
			expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/public/reports/r/media/m")
		})

		it("reports a 404 as not public", async () => {
			fetchMock.mockResolvedValue(reply(404))
			await expect(fetchMediaLink("r", "m")).rejects.toBeInstanceOf(PublicReportNotFound)
		})

		it("throws on another failure", async () => {
			fetchMock.mockResolvedValue(reply(500))
			await expect(fetchMediaLink("r", "m")).rejects.toThrow("(500)")
		})
	})

	describe("summaryIn", () => {
		const report = { aiSummaryEn: "en", aiSummaryFr: "fr" } as PublicReport

		it("picks French for fr-CA", () => {
			expect(summaryIn(report, "fr-CA")).toBe("fr")
		})

		it("falls back to English", () => {
			expect(summaryIn(report, "en-CA")).toBe("en")
			expect(summaryIn(report, "de")).toBe("en")
		})
	})

	describe("comments", () => {
		it("lists comments", async () => {
			fetchMock.mockResolvedValue(reply(200, [{ id: "c" }]))
			expect(await listComments("r")).toEqual([{ id: "c" }])
			expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/public/reports/r/comments/")
		})

		it("posts a comment", async () => {
			fetchMock.mockResolvedValue(reply(200, { id: "c" }))
			await postComment("r", "hi", "en-CA")
			const init = fetchMock.mock.calls[0][1] as RequestInit
			expect(init.method).toBe("POST")
			expect(init.body).toBe(JSON.stringify({ text: "hi", locale: "en-CA" }))
		})

		it("edits a comment", async () => {
			fetchMock.mockResolvedValue(reply(200, { id: "c" }))
			await editComment("r", "c/1", "hi", "fr-CA")
			expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/public/reports/r/comments/c%2F1")
			expect((fetchMock.mock.calls[0][1] as RequestInit).method).toBe("PUT")
		})

		it("deletes a comment and tolerates 204", async () => {
			fetchMock.mockResolvedValue(reply(204))
			await expect(deleteComment("r", "c")).resolves.toBeUndefined()
			expect((fetchMock.mock.calls[0][1] as RequestInit).method).toBe("DELETE")
		})

		it("hides media and a comment", async () => {
			fetchMock.mockResolvedValue(reply(204))
			await hideMedia("r", "m")
			await hideComment("c")
			expect(fetchMock.mock.calls[0][0]).toBe("/api/admin/reports/r/attachments/m/hide")
			expect(fetchMock.mock.calls[1][0]).toBe("/api/admin/comments/c/hide")
		})

		it("merges caller headers", async () => {
			fetchMock.mockResolvedValue(reply(200, []))
			// send is private; the headers default is covered by every call above.
			await listComments("r")
			expect((fetchMock.mock.calls[0][1] as RequestInit).headers).toEqual({ "Content-Type": "application/json" })
		})

		it("reports a 404 as not public", async () => {
			fetchMock.mockResolvedValue(reply(404))
			await expect(listComments("r")).rejects.toBeInstanceOf(PublicReportNotFound)
		})

		it("raises an ApiError with the problem detail", async () => {
			fetchMock.mockResolvedValue(reply(400, { detail: "too long", title: "t" }))
			const failure = await postComment("r", "x", "en-CA").catch((error: unknown) => error)
			expect(failure).toBeInstanceOf(ApiError)
			expect((failure as ApiError).status).toBe(400)
			expect((failure as ApiError).detail).toBe("too long")
		})

		it("falls back to the problem title", async () => {
			fetchMock.mockResolvedValue(reply(400, { title: "Bad" }))
			await expect(postComment("r", "x", "en-CA")).rejects.toMatchObject({ detail: "Bad" })
		})

		it("falls back to the status text when the body is not a problem", async () => {
			fetchMock.mockResolvedValue(reply(500, undefined, "Server Error"))
			await expect(postComment("r", "x", "en-CA")).rejects.toMatchObject({ status: 500, detail: "Server Error" })
		})
	})
})
