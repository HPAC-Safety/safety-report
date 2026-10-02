import { afterEach, describe, expect, it, vi } from "vitest"

import { fetchCurrentQuestions } from "./publicQuestions"

afterEach(() => {
	vi.unstubAllGlobals()
})

describe("fetchCurrentQuestions", () => {
	it("returns the current question set", async () => {
		const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify([{ id: "q" }]), { status: 200 }))
		vi.stubGlobal("fetch", fetchMock)

		await expect(fetchCurrentQuestions()).resolves.toEqual([{ id: "q" }])
		expect(fetchMock).toHaveBeenCalledWith("/api/v1/questions/")
	})

	it("throws naming the status when the set cannot be loaded", async () => {
		vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("", { status: 503 })))

		await expect(fetchCurrentQuestions()).rejects.toThrow("The current question set could not be loaded (503).")
	})
})
