import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"

vi.mock("../auth/session", () => ({
	readSession: vi.fn(() => ({ accessToken: "tok" })),
	clearSession: vi.fn(),
}))

import { SubmissionNetworkError, SubmissionRejectedError, submitReport } from "./reportSubmission"

const fetchMock = vi.fn()

function problem(body: unknown, status = 400): Response {
	return new Response(JSON.stringify(body), { status })
}

beforeEach(() => {
	fetchMock.mockReset()
	vi.stubGlobal("fetch", fetchMock)
})

afterEach(() => {
	vi.unstubAllGlobals()
})

describe("error classes", () => {
	it("defaults a rejection to no uploads", () => {
		const error = new SubmissionRejectedError("no")
		expect(error.detail).toBe("no")
		expect(error.expiredUploadIds).toEqual([])
		expect(error.refusedUploads).toEqual([])
		expect(error.name).toBe("SubmissionRejectedError")
	})

	it("describes a network failure", () => {
		const error = new SubmissionNetworkError()
		expect(error.message).toBe("The report could not be sent.")
		expect(error.name).toBe("SubmissionNetworkError")
	})
})

describe("submitReport", () => {
	it("posts the answers and returns the accepted report", async () => {
		fetchMock.mockResolvedValue(problem({ id: "r1", status: "Submitted" }, 202))

		await expect(submitReport("en-CA", [])).resolves.toEqual({ id: "r1", status: "Submitted" })

		const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
		expect(path).toBe("/api/v1/reports/")
		expect(init.method).toBe("POST")
		expect(init.headers).toEqual({ Authorization: "Bearer tok", "Content-Type": "application/json" })
		expect(init.body).toBe(JSON.stringify({ language: "en-CA", answers: [] }))
	})

	it("throws a network error when the request never gets a response", async () => {
		fetchMock.mockRejectedValue(new TypeError("offline"))
		await expect(submitReport("en-CA", [])).rejects.toBeInstanceOf(SubmissionNetworkError)
	})

	it("throws a rejection carrying the detail and upload lists", async () => {
		const refused = [{ uploadId: "u2", reason: "type" }]
		fetchMock.mockResolvedValue(
			problem({ detail: "d", title: "t", expiredUploadIds: ["u1"], refusedUploads: refused }),
		)
		await expect(submitReport("fr-CA", [])).rejects.toMatchObject({
			detail: "d",
			expiredUploadIds: ["u1"],
			refusedUploads: refused,
		})
	})

	it("falls back to the title", async () => {
		fetchMock.mockResolvedValue(problem({ title: "t" }))
		await expect(submitReport("en-CA", [])).rejects.toMatchObject({ detail: "t", expiredUploadIds: [] })
	})

	it("falls back to a generic message for a non-JSON body", async () => {
		fetchMock.mockResolvedValue(new Response("oops", { status: 500 }))
		const error = await submitReport("en-CA", []).catch((e: unknown) => e)
		expect(error).toBeInstanceOf(SubmissionRejectedError)
		expect(error).toMatchObject({
			detail: "That submission was not accepted.",
			expiredUploadIds: [],
			refusedUploads: [],
		})
	})

	it("ignores upload lists that are not arrays", async () => {
		fetchMock.mockResolvedValue(problem({ expiredUploadIds: "x", refusedUploads: {} }))
		await expect(submitReport("en-CA", [])).rejects.toMatchObject({ expiredUploadIds: [], refusedUploads: [] })
	})
})
