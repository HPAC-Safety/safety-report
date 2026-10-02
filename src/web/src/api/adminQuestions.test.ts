import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"

vi.mock("../auth/session", () => ({
	readSession: vi.fn(),
	clearSession: vi.fn(),
}))

import { clearSession, readSession, type MemberSession } from "../auth/session"
import {
	ApiError,
	approveTypeAheadValue,
	authorization,
	correctTypeAheadValue,
	createQuestion,
	deleteQuestion,
	listQuestions,
	listTypeAheadValuesAwaitingReview,
	mergeTypeAheadValue,
	removeTypeAheadValue,
	reorderQuestions,
	reviseQuestion,
	setTypeAheadValueParents,
	translatableByDefault,
	translate,
	translationAvailable,
	type SaveQuestionRequest,
} from "./adminQuestions"

const fetchMock = vi.fn()

function jsonResponse(body: unknown, status = 200, statusText = ""): Response {
	return new Response(JSON.stringify(body), { status, statusText })
}

function lastCall(): { path: string; init: RequestInit } {
	const [path, init] = fetchMock.mock.calls[fetchMock.mock.calls.length - 1] as [string, RequestInit]
	return { path, init }
}

const request = { type: "short_text", options: [] } as unknown as SaveQuestionRequest

beforeEach(() => {
	fetchMock.mockReset()
	vi.stubGlobal("fetch", fetchMock)
	vi.mocked(readSession).mockReturnValue({ accessToken: "tok" } as unknown as MemberSession)
	vi.mocked(clearSession).mockClear()
})

afterEach(() => {
	vi.unstubAllGlobals()
})

describe("translatableByDefault", () => {
	it("is true only for long text", () => {
		expect(translatableByDefault("long_text")).toBe(true)
		expect(translatableByDefault("short_text")).toBe(false)
	})
})

describe("authorization", () => {
	it("sends a bearer header when signed in", () => {
		expect(authorization()).toEqual({ Authorization: "Bearer tok" })
	})

	it("sends no header when signed out", () => {
		vi.mocked(readSession).mockReturnValue(null)
		expect(authorization()).toEqual({})
	})
})

describe("ApiError", () => {
	it("carries status, detail and an optional type", () => {
		const plain = new ApiError(400, "bad")
		expect(plain.status).toBe(400)
		expect(plain.detail).toBe("bad")
		expect(plain.type).toBeNull()
		expect(plain.name).toBe("ApiError")
		expect(new ApiError(409, "stale", "t").type).toBe("t")
	})
})

describe("question calls", () => {
	it("lists questions with JSON and bearer headers", async () => {
		fetchMock.mockResolvedValue(jsonResponse([{ id: "1" }]))
		await expect(listQuestions()).resolves.toEqual([{ id: "1" }])
		const { path, init } = lastCall()
		expect(path).toBe("/api/admin/questions")
		expect(init.headers).toEqual({ "Content-Type": "application/json", Authorization: "Bearer tok" })
	})

	it("omits the bearer header when signed out", async () => {
		vi.mocked(readSession).mockReturnValue(null)
		fetchMock.mockResolvedValue(jsonResponse([]))
		await listQuestions()
		expect(lastCall().init.headers).toEqual({ "Content-Type": "application/json" })
	})

	it("creates, revises, reorders and deletes", async () => {
		fetchMock.mockImplementation(() => Promise.resolve(jsonResponse({ id: "1" })))
		await createQuestion(request)
		expect(lastCall().init.method).toBe("POST")
		expect(lastCall().init.body).toBe(JSON.stringify(request))

		await reviseQuestion("q1", request)
		expect(lastCall().path).toBe("/api/admin/questions/q1")
		expect(lastCall().init.method).toBe("PUT")

		await reorderQuestions(["a", "b"])
		expect(lastCall().path).toBe("/api/admin/questions/order")
		expect(lastCall().init.body).toBe(JSON.stringify({ questionIdsInOrder: ["a", "b"] }))

		fetchMock.mockResolvedValue(new Response(null, { status: 204 }))
		await expect(deleteQuestion("q1")).resolves.toBeUndefined()
		expect(lastCall().init.method).toBe("DELETE")
	})

	it("asks about and performs translation", async () => {
		fetchMock.mockResolvedValue(jsonResponse({ available: true }))
		await expect(translationAvailable()).resolves.toEqual({ available: true })
		expect(lastCall().path).toBe("/api/admin/translate")

		fetchMock.mockResolvedValue(jsonResponse({ texts: ["b"] }))
		await expect(translate(["a"], "en", "fr")).resolves.toEqual({ texts: ["b"] })
		expect(lastCall().init.body).toBe(JSON.stringify({ texts: ["a"], from: "en", to: "fr" }))
	})

	it("lists type-ahead values awaiting review", async () => {
		fetchMock.mockResolvedValue(jsonResponse({ values: [], count: 0 }))
		await expect(listTypeAheadValuesAwaitingReview()).resolves.toEqual({ values: [], count: 0 })
		expect(lastCall().path).toBe("/api/admin/type-ahead-values/awaiting-review")
	})

	it("drives each type-ahead value action", async () => {
		fetchMock.mockImplementation(() => Promise.resolve(new Response(null, { status: 204 })))
		await approveTypeAheadValue("a/b")
		expect(lastCall().path).toBe("/api/admin/type-ahead-values/a%2Fb/approval")
		expect(lastCall().init.method).toBe("POST")

		await correctTypeAheadValue("v", "en", "fr")
		expect(lastCall().path).toBe("/api/admin/type-ahead-values/v")
		expect(lastCall().init.body).toBe(JSON.stringify({ labelEn: "en", labelFr: "fr" }))

		await mergeTypeAheadValue("v", "w")
		expect(lastCall().path).toBe("/api/admin/type-ahead-values/v/merge")
		expect(lastCall().init.body).toBe(JSON.stringify({ intoId: "w" }))

		await setTypeAheadValueParents("v", ["p"])
		expect(lastCall().path).toBe("/api/admin/type-ahead-values/v/parent")
		expect(lastCall().init.body).toBe(JSON.stringify({ parentChoiceIds: ["p"] }))

		await removeTypeAheadValue("v")
		expect(lastCall().init.method).toBe("DELETE")
	})
})

describe("failures", () => {
	it("throws the problem detail", async () => {
		fetchMock.mockResolvedValue(jsonResponse({ detail: "d", title: "t" }, 400))
		await expect(listQuestions()).rejects.toMatchObject({ status: 400, detail: "d" })
	})

	it("falls back to the problem title", async () => {
		fetchMock.mockResolvedValue(jsonResponse({ title: "t" }, 400))
		await expect(listQuestions()).rejects.toMatchObject({ detail: "t" })
	})

	it("falls back to the status text for a non-JSON body", async () => {
		fetchMock.mockResolvedValue(new Response("nope", { status: 500, statusText: "Boom" }))
		await expect(listQuestions()).rejects.toMatchObject({ status: 500, detail: "Boom" })
	})

	it("clears the session on a 401", async () => {
		fetchMock.mockResolvedValue(jsonResponse({ title: "no" }, 401))
		await expect(listQuestions()).rejects.toBeInstanceOf(ApiError)
		expect(clearSession).toHaveBeenCalledOnce()
	})

	it("keeps the session on other failures", async () => {
		fetchMock.mockResolvedValue(jsonResponse({ title: "no" }, 403))
		await expect(listQuestions()).rejects.toBeInstanceOf(ApiError)
		expect(clearSession).not.toHaveBeenCalled()
	})
})
