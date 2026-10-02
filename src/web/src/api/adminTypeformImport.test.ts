import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"

vi.mock("../auth/session", () => ({
	readSession: vi.fn(() => null),
	clearSession: vi.fn(),
}))

import { clearSession } from "../auth/session"
import { ApiError } from "./adminQuestions"
import {
	deletePendingImportLogic,
	exportTypeform,
	importTypeform,
	listPendingImportLogic,
} from "./adminTypeformImport"

const fetchMock = vi.fn()

function jsonResponse(body: unknown, status = 200, statusText = ""): Response {
	return new Response(JSON.stringify(body), { status, statusText })
}

beforeEach(() => {
	fetchMock.mockReset()
	vi.stubGlobal("fetch", fetchMock)
	vi.mocked(clearSession).mockClear()
})

afterEach(() => {
	vi.unstubAllGlobals()
})

const failing: [string, () => Promise<unknown>][] = [
	["importTypeform", () => importTypeform(new File(["a"], "en.json"), new File(["b"], "fr.json"))],
	["listPendingImportLogic", () => listPendingImportLogic()],
	["exportTypeform", () => exportTypeform()],
	["deletePendingImportLogic", () => deletePendingImportLogic("x")],
]

describe("importTypeform", () => {
	it("posts both files as multipart form data", async () => {
		const preview = { drafts: [], rejected: [], pendingLogicNoteIds: [] }
		fetchMock.mockResolvedValue(jsonResponse(preview))
		const english = new File(["a"], "en.json")
		const french = new File(["b"], "fr.json")

		await expect(importTypeform(english, french)).resolves.toEqual(preview)

		const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
		expect(path).toBe("/api/admin/typeform/import")
		expect(init.method).toBe("POST")
		const body = init.body as FormData
		expect((body.get("english") as File).name).toBe("en.json")
		expect((body.get("french") as File).name).toBe("fr.json")
	})
})

describe("listPendingImportLogic", () => {
	it("returns the pending notes", async () => {
		fetchMock.mockResolvedValue(jsonResponse([{ id: "1" }]))
		await expect(listPendingImportLogic()).resolves.toEqual([{ id: "1" }])
		expect(fetchMock.mock.calls[0][0]).toBe("/api/admin/typeform/pending-logic")
	})
})

describe("exportTypeform", () => {
	it("returns the zip as a blob", async () => {
		fetchMock.mockResolvedValue(new Response("zip", { status: 200 }))
		const blob = await exportTypeform()
		expect(await blob.text()).toBe("zip")
	})
})

describe("deletePendingImportLogic", () => {
	it("deletes one note", async () => {
		fetchMock.mockResolvedValue(new Response(null, { status: 204 }))
		await expect(deletePendingImportLogic("n1")).resolves.toBeUndefined()
		const [path, init] = fetchMock.mock.calls[0] as [string, RequestInit]
		expect(path).toBe("/api/admin/typeform/pending-logic/n1")
		expect(init.method).toBe("DELETE")
	})
})

describe.each(failing)("%s failures", (_name, run) => {
	it("throws the problem detail", async () => {
		fetchMock.mockResolvedValue(jsonResponse({ detail: "d", title: "t" }, 400))
		await expect(run()).rejects.toMatchObject({ status: 400, detail: "d" })
	})

	it("falls back to the problem title", async () => {
		fetchMock.mockResolvedValue(jsonResponse({ title: "t" }, 400))
		await expect(run()).rejects.toMatchObject({ detail: "t" })
	})

	it("falls back to the status text for a non-JSON body", async () => {
		fetchMock.mockResolvedValue(new Response("x", { status: 500, statusText: "Boom" }))
		const error = await run().catch((e: unknown) => e)
		expect(error).toBeInstanceOf(ApiError)
		expect(error).toMatchObject({ detail: "Boom" })
	})
})

describe("session clearing", () => {
	it("clears the session on a 401 for import and export", async () => {
		fetchMock.mockResolvedValue(jsonResponse({ title: "no" }, 401))
		await expect(failing[0][1]()).rejects.toBeInstanceOf(ApiError)
		await expect(failing[2][1]()).rejects.toBeInstanceOf(ApiError)
		expect(clearSession).toHaveBeenCalledTimes(2)
	})
})
