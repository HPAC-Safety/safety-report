import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"

vi.mock("../auth/session", () => ({
	readSession: vi.fn(() => ({ accessToken: "tok" })),
	clearSession: vi.fn(),
}))

import { clearSession } from "../auth/session"
import { ApiError } from "./adminQuestions"
import {
	PrivateUploadError,
	REPORT_FILTERS,
	addPrivateAttachment,
	addPrivateNote,
	attachmentLink,
	attachmentOriginalLink,
	consentKey,
	deleteReport,
	editPrivateNote,
	getPendingCounts,
	getReport,
	isReportFilter,
	listPrivateAttachments,
	listPrivateNotes,
	listReports,
	privateAttachmentLink,
	privateNoteHistory,
	publishReport,
	removePrivateAttachment,
	removePrivateNote,
	rollBackSummary,
	saveSummaryPair,
	setAttachmentHidden,
	stagePrivateUpload,
	unpublishReport,
	type PrivateNote,
	type ReportAttachment,
} from "./adminReports"

const fetchMock = vi.fn()

function reply(status: number, body: unknown = {}, statusText = "Text"): Response {
	return {
		status,
		statusText,
		ok: status >= 200 && status < 300,
		json: async () => body,
	} as unknown as Response
}

function lastCall(): { path: string; init: RequestInit & { headers: Record<string, string> } } {
	const call = fetchMock.mock.calls.at(-1) as [string, RequestInit & { headers: Record<string, string> }]
	return { path: call[0], init: call[1] }
}

class FakeXhr {
	static instances: FakeXhr[] = []
	method = ""
	url = ""
	headers: Record<string, string> = {}
	status = 200
	sent: unknown = null
	aborted = false
	upload: { onprogress: ((event: { lengthComputable: boolean; loaded: number; total: number }) => void) | null } = {
		onprogress: null,
	}
	onload: (() => void) | null = null
	onerror: (() => void) | null = null
	onabort: (() => void) | null = null
	constructor() {
		FakeXhr.instances.push(this)
	}
	open(method: string, url: string) {
		this.method = method
		this.url = url
	}
	setRequestHeader(name: string, value: string) {
		this.headers[name] = value
	}
	send(body: unknown) {
		this.sent = body
	}
	abort() {
		this.aborted = true
		this.onabort?.()
	}
}

beforeEach(() => {
	fetchMock.mockReset()
	FakeXhr.instances = []
	vi.mocked(clearSession).mockClear()
	vi.stubGlobal("fetch", fetchMock)
	vi.stubGlobal("XMLHttpRequest", FakeXhr)
})

afterEach(() => {
	vi.unstubAllGlobals()
})

describe("consentKey", () => {
	it("names each consent", () => {
		expect(consentKey(null)).toBe("unanswered")
		expect(consentKey(true)).toBe("yes")
		expect(consentKey(false)).toBe("no")
	})
})

describe("isReportFilter", () => {
	it("accepts every listed filter", () => {
		for (const filter of REPORT_FILTERS) expect(isReportFilter(filter)).toBe(true)
	})

	it("refuses null and unknown values", () => {
		expect(isReportFilter(null)).toBe(false)
		expect(isReportFilter("nope")).toBe(false)
	})
})

describe("request plumbing", () => {
	it("sends the bearer token and JSON content type", async () => {
		fetchMock.mockResolvedValueOnce(reply(200, { items: [], next: null }))
		await listReports("all")
		expect(lastCall().init.headers).toMatchObject({ "Content-Type": "application/json", Authorization: "Bearer tok" })
	})

	it("clears the session on 401 and throws the problem detail", async () => {
		fetchMock.mockResolvedValueOnce(reply(401, { detail: "no", title: "t", type: "x" }))
		await expect(getReport("1")).rejects.toMatchObject({ status: 401, detail: "no", type: "x" })
		expect(clearSession).toHaveBeenCalled()
	})

	it("falls back to the title, then the status text", async () => {
		fetchMock.mockResolvedValueOnce(reply(400, { title: "Title" }))
		await expect(getReport("1")).rejects.toMatchObject({ detail: "Title", type: null })
		fetchMock.mockResolvedValueOnce(reply(500, {}, "Server Error"))
		await expect(getReport("1")).rejects.toBeInstanceOf(ApiError)
		fetchMock.mockResolvedValueOnce(reply(500, {}, "Server Error"))
		await expect(getReport("1")).rejects.toMatchObject({ detail: "Server Error" })
	})

	it("survives an unreadable error body", async () => {
		fetchMock.mockResolvedValueOnce({
			status: 502,
			statusText: "Bad Gateway",
			ok: false,
			json: async () => {
				throw new Error("bad")
			},
		})
		await expect(getReport("1")).rejects.toMatchObject({ status: 502, detail: "Bad Gateway" })
		expect(clearSession).not.toHaveBeenCalled()
	})

	it("returns undefined for 204", async () => {
		fetchMock.mockResolvedValueOnce(reply(204))
		await expect(deleteReport("1")).resolves.toBeUndefined()
		expect(lastCall().init.method).toBe("DELETE")
	})
})

describe("report commands", () => {
	beforeEach(() => {
		fetchMock.mockResolvedValue(reply(200, { ok: true }))
	})

	it("lists with filter, cursor and trimmed search", async () => {
		await listReports("published", "abc", "  fuel ")
		expect(lastCall().path).toBe("/api/admin/reports?filter=published&after=abc&q=fuel")
	})

	it("lists with only a filter", async () => {
		await listReports("all", null, "   ")
		expect(lastCall().path).toBe("/api/admin/reports?filter=all")
	})

	it("gets a report by encoded id", async () => {
		await getReport("a/b")
		expect(lastCall().path).toBe("/api/admin/reports/a%2Fb")
	})

	it("saves a summary pair with default human sources", async () => {
		await saveSummaryPair("1", "v", "en", "fr")
		expect(lastCall().init.method).toBe("PUT")
		expect(JSON.parse(lastCall().init.body as string)).toEqual({
			version: "v",
			aiSummaryEn: "en",
			aiSummaryFr: "fr",
			sourceEn: "human",
			sourceFr: "human",
		})
	})

	it("saves a summary pair with given sources", async () => {
		await saveSummaryPair("1", "v", "en", "fr", "machine", "generated")
		expect(JSON.parse(lastCall().init.body as string)).toMatchObject({ sourceEn: "machine", sourceFr: "generated" })
	})

	it("rolls back, publishes and unpublishes", async () => {
		await rollBackSummary("1", "v", "r/1")
		expect(lastCall().path).toBe("/api/admin/reports/1/summary/revisions/r%2F1/rollback")
		await publishReport("1", "v")
		expect(lastCall().path).toBe("/api/admin/reports/1/publish")
		await unpublishReport("1", "v", "note")
		expect(JSON.parse(lastCall().init.body as string)).toEqual({ version: "v", note: "note" })
	})

	it("hides and shows an attachment", async () => {
		await setAttachmentHidden("1", "a", true)
		expect(lastCall().path).toBe("/api/admin/reports/1/attachments/a/hide")
		await setAttachmentHidden("1", "a", false)
		expect(lastCall().path).toBe("/api/admin/reports/1/attachments/a/show")
	})

	it("links a document to download and media to view", async () => {
		const base = { id: "a", state: "ready", visibility: "public", format: null } as const
		await attachmentLink("1", { ...base, kind: "document" } as ReportAttachment)
		expect(lastCall().path).toBe("/api/admin/reports/1/attachments/a/download")
		await attachmentLink("1", { ...base, kind: "image" } as ReportAttachment)
		expect(lastCall().path).toBe("/api/admin/reports/1/attachments/a/view")
	})

	it("links an original", async () => {
		await attachmentOriginalLink("1", "a")
		expect(lastCall().path).toBe("/api/admin/reports/1/attachments/a/original")
	})

	it("gets pending counts", async () => {
		await getPendingCounts()
		expect(lastCall().path).toBe("/api/admin/counts")
	})
})

describe("private notes", () => {
	beforeEach(() => {
		fetchMock.mockResolvedValue(reply(200, []))
	})

	it("lists, adds, edits, removes and reads history", async () => {
		await listPrivateNotes("1")
		expect(lastCall().path).toBe("/api/admin/reports/1/private-notes")
		await addPrivateNote("1", "t")
		expect(JSON.parse(lastCall().init.body as string)).toEqual({ text: "t", attachmentId: null })
		await addPrivateNote("1", "t", "att")
		expect(JSON.parse(lastCall().init.body as string)).toEqual({ text: "t", attachmentId: "att" })
		const note = { id: "n/1", revision: 2 } as PrivateNote
		await editPrivateNote("1", note, "x")
		expect(lastCall().path).toBe("/api/admin/reports/1/private-notes/n%2F1")
		expect(JSON.parse(lastCall().init.body as string)).toEqual({ text: "x", revision: 2, attachmentId: null })
		await editPrivateNote("1", note, "x", "att")
		expect(JSON.parse(lastCall().init.body as string)).toMatchObject({ attachmentId: "att" })
		await removePrivateNote("1", "n")
		expect(lastCall().init.method).toBe("DELETE")
		await privateNoteHistory("1", "n")
		expect(lastCall().path).toBe("/api/admin/reports/1/private-notes/n/revisions")
	})
})

describe("private attachments", () => {
	it("lists, removes and links", async () => {
		fetchMock.mockResolvedValue(reply(200, []))
		await listPrivateAttachments("1")
		expect(lastCall().path).toBe("/api/admin/reports/1/private-attachments")
		await removePrivateAttachment("1", "a")
		expect(lastCall().path).toBe("/api/admin/reports/1/private-attachments/a")
		await privateAttachmentLink("1", "a")
		expect(lastCall().path).toBe("/api/admin/reports/1/private-attachments/a/download")
	})

	it("carries the reason on a PrivateUploadError", () => {
		const error = new PrivateUploadError("network")
		expect(error.reason).toBe("network")
		expect(error.name).toBe("PrivateUploadError")
	})

	describe("addPrivateAttachment", () => {
		it("claims the upload with a trimmed description", async () => {
			fetchMock.mockResolvedValueOnce(reply(200, { id: "a" }))
			await expect(addPrivateAttachment("1", "u", "f.pdf", "  d ")).resolves.toEqual({ id: "a" })
			expect(JSON.parse(lastCall().init.body as string)).toEqual({ uploadId: "u", fileName: "f.pdf", description: "d" })
		})

		it("sends a null for a blank description", async () => {
			fetchMock.mockResolvedValueOnce(reply(200, { id: "a" }))
			await addPrivateAttachment("1", "u", "f.pdf", "  ")
			expect(JSON.parse(lastCall().init.body as string).description).toBeNull()
		})

		it("erases the upload and reports a claim failure", async () => {
			fetchMock.mockResolvedValueOnce(reply(409)).mockResolvedValueOnce(reply(204))
			await expect(addPrivateAttachment("1", "u", "f", "d")).rejects.toMatchObject({ reason: "claim" })
			expect(lastCall().path).toBe("/api/v1/uploads/u")
		})
	})

	describe("stagePrivateUpload", () => {
		const minted = { uploadId: "u1", contentType: "application/pdf", uploadUrl: "https://s3/put" }
		const pdf = new File(["abc"], "a.pdf", { type: "application/pdf" })

		async function settle() {
			await vi.waitFor(() => expect(FakeXhr.instances).toHaveLength(1))
			return FakeXhr.instances[0] as FakeXhr
		}

		it("mints, PUTs with progress and returns the staged upload", async () => {
			fetchMock.mockResolvedValueOnce(reply(200, minted))
			const progress = vi.fn()
			const promise = stagePrivateUpload("1", pdf, progress, new AbortController().signal)
			const xhr = await settle()
			expect(xhr.method).toBe("PUT")
			expect(xhr.url).toBe("https://s3/put")
			expect(xhr.headers["Content-Type"]).toBe("application/pdf")
			expect(xhr.sent).toBe(pdf)
			xhr.upload.onprogress?.({ lengthComputable: true, loaded: 1, total: 4 })
			xhr.upload.onprogress?.({ lengthComputable: false, loaded: 1, total: 0 })
			expect(progress).toHaveBeenCalledTimes(1)
			expect(progress).toHaveBeenCalledWith(0.25)
			xhr.status = 204
			xhr.onload?.()
			await expect(promise).resolves.toEqual({ uploadId: "u1", contentType: "application/pdf" })
		})

		it("reports an oversized file when the API refuses with 400", async () => {
			fetchMock.mockResolvedValueOnce(reply(400, {}))
			await expect(stagePrivateUpload("1", pdf, vi.fn(), new AbortController().signal)).rejects.toMatchObject({
				reason: "too_large",
			})
		})

		it("reports an empty file when the API refuses with 400", async () => {
			fetchMock.mockResolvedValueOnce(reply(400, {}))
			const empty = new File([], "e.pdf", { type: "application/pdf" })
			await expect(stagePrivateUpload("1", empty, vi.fn(), new AbortController().signal)).rejects.toMatchObject({
				reason: "empty",
			})
		})

		it("rethrows another ApiError", async () => {
			fetchMock.mockResolvedValueOnce(reply(500, {}))
			await expect(stagePrivateUpload("1", pdf, vi.fn(), new AbortController().signal)).rejects.toMatchObject({
				status: 500,
			})
		})

		it("rethrows a non-API failure", async () => {
			const boom = new Error("offline")
			fetchMock.mockRejectedValueOnce(boom)
			await expect(stagePrivateUpload("1", pdf, vi.fn(), new AbortController().signal)).rejects.toBe(boom)
		})

		it("reports a storage failure and erases the upload", async () => {
			fetchMock.mockResolvedValueOnce(reply(200, minted)).mockResolvedValueOnce(reply(204))
			const promise = stagePrivateUpload("1", pdf, vi.fn(), new AbortController().signal)
			const xhr = await settle()
			xhr.status = 403
			xhr.onload?.()
			await expect(promise).rejects.toMatchObject({ reason: "storage" })
			expect(lastCall().path).toBe("/api/v1/uploads/u1")
		})

		it("reports a network failure", async () => {
			fetchMock.mockResolvedValueOnce(reply(200, minted)).mockResolvedValue(reply(204))
			const promise = stagePrivateUpload("1", pdf, vi.fn(), new AbortController().signal)
			const xhr = await settle()
			xhr.onerror?.()
			await expect(promise).rejects.toMatchObject({ reason: "network" })
		})

		it("aborts the request when the signal fires", async () => {
			fetchMock.mockResolvedValue(reply(200, minted))
			const controller = new AbortController()
			const promise = stagePrivateUpload("1", pdf, vi.fn(), controller.signal)
			const xhr = await settle()
			controller.abort()
			await expect(promise).rejects.toMatchObject({ name: "AbortError" })
			expect(xhr.aborted).toBe(true)
		})
	})
})
