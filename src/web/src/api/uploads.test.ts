import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"

vi.mock("../auth/session", () => ({
	readSession: vi.fn(() => ({ accessToken: "tok" })),
	clearSession: vi.fn(),
}))

import { readSession } from "../auth/session"
import {
	ACCEPTED_FILE_TYPES,
	MAX_BYTES_BY_KIND,
	SIZE_LIMIT_PARAMS,
	UploadNetworkError,
	UploadRejectedError,
	declaredKind,
	declaredType,
	deleteUpload,
	exceedsKindLimit,
	uploadAttachment,
} from "./uploads"

const fetchMock = vi.fn()

function file(name: string, type: string, size = 3): File {
	const f = new File(["abc"], name, { type })
	if (size !== 3) Object.defineProperty(f, "size", { value: size })
	return f
}

function json(status: number, body: unknown): Response {
	return { status, ok: status >= 200 && status < 300, json: async () => body } as unknown as Response
}

function brokenJson(status: number): Response {
	return {
		status,
		ok: false,
		json: async () => {
			throw new Error("bad")
		},
	} as unknown as Response
}

beforeEach(() => {
	fetchMock.mockReset()
	vi.stubGlobal("fetch", fetchMock)
})

afterEach(() => {
	vi.unstubAllGlobals()
})

describe("constants", () => {
	it("lists extensions and types for the picker", () => {
		expect(ACCEPTED_FILE_TYPES).toContain(".pdf")
		expect(ACCEPTED_FILE_TYPES).toContain("image/png")
	})

	it("states the limits in whole megabytes", () => {
		expect(SIZE_LIMIT_PARAMS).toEqual({ videoSize: 250, size: 25 })
	})
})

describe("error classes", () => {
	it("carries the rejection reason", () => {
		const error = new UploadRejectedError("too_large")
		expect(error.reason).toBe("too_large")
		expect(error.name).toBe("UploadRejectedError")
	})

	it("names a network failure", () => {
		expect(new UploadNetworkError().name).toBe("UploadNetworkError")
	})
})

describe("declaredType", () => {
	it("lower-cases and strips parameters", () => {
		expect(declaredType(file("a.bin", "Image/PNG; charset=x"))).toBe("image/png")
	})

	it("falls back to the extension when the browser declares nothing", () => {
		expect(declaredType(file("photo.HEIC", ""))).toBe("image/heic")
	})

	it("falls back to octet-stream for an unknown extension", () => {
		expect(declaredType(file("thing.xyz", ""))).toBe("application/octet-stream")
	})

	it("falls back to octet-stream for a name without extension", () => {
		expect(declaredType(file("", ""))).toBe("application/octet-stream")
	})
})

describe("declaredKind", () => {
	it("classifies images, videos and documents", () => {
		expect(declaredKind("image/png")).toBe("image")
		expect(declaredKind("video/mp4")).toBe("video")
		expect(declaredKind("application/pdf")).toBe("document")
		expect(declaredKind("text/rtf")).toBe("document")
	})

	it("refuses a type the form does not offer", () => {
		expect(declaredKind("application/zip")).toBeNull()
	})
})

describe("exceedsKindLimit", () => {
	it("is true for an oversized image", () => {
		expect(exceedsKindLimit(file("a.png", "image/png", MAX_BYTES_BY_KIND.image + 1))).toBe(true)
	})

	it("is false within the limit", () => {
		expect(exceedsKindLimit(file("a.png", "image/png"))).toBe(false)
	})

	it("is false for an unknown kind", () => {
		expect(exceedsKindLimit(file("a.zip", "application/zip", 10 ** 10))).toBe(false)
	})
})

describe("uploadAttachment", () => {
	const minted = { uploadId: "u1", kind: "image", uploadUrl: "https://s3/put" }

	it("mints then PUTs the file", async () => {
		fetchMock.mockResolvedValueOnce(json(201, minted)).mockResolvedValueOnce(json(200, null))
		const result = await uploadAttachment(file("a.png", "image/png"), new AbortController().signal)
		expect(result).toEqual({ uploadId: "u1", kind: "image" })
		expect(fetchMock.mock.calls[0]?.[1].headers).toMatchObject({ Authorization: "Bearer tok" })
		expect(fetchMock.mock.calls[1]?.[0]).toBe("https://s3/put")
	})

	it("sends no Authorization header when signed out", async () => {
		vi.mocked(readSession).mockReturnValueOnce(null)
		fetchMock.mockResolvedValueOnce(json(201, minted)).mockResolvedValueOnce(json(200, null))
		await uploadAttachment(file("a.png", "image/png"), new AbortController().signal)
		expect(fetchMock.mock.calls[0]?.[1].headers.Authorization).toBeUndefined()
	})

	it("reports a network failure while minting", async () => {
		fetchMock.mockRejectedValueOnce(new Error("offline"))
		await expect(uploadAttachment(file("a.png", "image/png"), new AbortController().signal)).rejects.toBeInstanceOf(
			UploadNetworkError,
		)
	})

	it("rethrows the abort while minting", async () => {
		const controller = new AbortController()
		const abort = new DOMException("Aborted", "AbortError")
		fetchMock.mockImplementationOnce(async () => {
			controller.abort()
			throw abort
		})
		await expect(uploadAttachment(file("a.png", "image/png"), controller.signal)).rejects.toBe(abort)
	})

	it("surfaces the API's rejection reason", async () => {
		fetchMock.mockResolvedValueOnce(json(400, { reason: "too_large" }))
		await expect(uploadAttachment(file("a.png", "image/png"), new AbortController().signal)).rejects.toMatchObject({
			reason: "too_large",
		})
	})

	it("treats a reasonless problem as unknown", async () => {
		fetchMock.mockResolvedValueOnce(json(400, {}))
		await expect(uploadAttachment(file("a.png", "image/png"), new AbortController().signal)).rejects.toMatchObject({
			reason: "unknown",
		})
	})

	it("treats a malformed body as unknown", async () => {
		fetchMock.mockResolvedValueOnce(brokenJson(500))
		await expect(uploadAttachment(file("a.png", "image/png"), new AbortController().signal)).rejects.toMatchObject({
			reason: "unknown",
		})
	})

	it("releases the upload and reports a network failure when the PUT fails", async () => {
		fetchMock
			.mockResolvedValueOnce(json(201, minted))
			.mockRejectedValueOnce(new Error("offline"))
			.mockResolvedValueOnce(json(204, null))
		await expect(uploadAttachment(file("a.png", "image/png"), new AbortController().signal)).rejects.toBeInstanceOf(
			UploadNetworkError,
		)
		expect(fetchMock.mock.calls[2]?.[0]).toBe("/api/v1/uploads/u1")
		expect(fetchMock.mock.calls[2]?.[1].method).toBe("DELETE")
	})

	it("rethrows the abort during the PUT", async () => {
		const controller = new AbortController()
		const abort = new DOMException("Aborted", "AbortError")
		fetchMock.mockResolvedValueOnce(json(201, minted)).mockImplementationOnce(async () => {
			controller.abort()
			throw abort
		})
		fetchMock.mockResolvedValue(json(204, null))
		await expect(uploadAttachment(file("a.png", "image/png"), controller.signal)).rejects.toBe(abort)
	})

	it("releases the upload when storage refuses it", async () => {
		fetchMock
			.mockResolvedValueOnce(json(201, minted))
			.mockResolvedValueOnce(json(403, null))
			.mockResolvedValueOnce(json(204, null))
		await expect(uploadAttachment(file("a.png", "image/png"), new AbortController().signal)).rejects.toMatchObject({
			reason: "unknown",
		})
		expect(fetchMock).toHaveBeenCalledTimes(3)
	})
})

describe("deleteUpload", () => {
	it("deletes the encoded upload id", async () => {
		fetchMock.mockResolvedValueOnce(json(204, null))
		await deleteUpload("a/b")
		expect(fetchMock.mock.calls[0]?.[0]).toBe("/api/v1/uploads/a%2Fb")
	})

	it("swallows a failure", async () => {
		fetchMock.mockRejectedValueOnce(new Error("offline"))
		await expect(deleteUpload("x")).resolves.toBeUndefined()
	})
})
