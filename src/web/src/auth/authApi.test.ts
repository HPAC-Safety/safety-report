import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"

type AuthApi = typeof import("./authApi")

function reply(ok: boolean, body: unknown = {}): Response {
	return { ok, json: () => Promise.resolve(body) } as unknown as Response
}

describe("authApi", () => {
	let api: AuthApi
	const fetchMock = vi.fn()

	beforeEach(async () => {
		vi.resetModules()
		fetchMock.mockReset()
		vi.stubGlobal("fetch", fetchMock)
		api = await import("./authApi")
	})
	afterEach(() => vi.unstubAllGlobals())

	describe("loadAuthConfig", () => {
		it("reads the configuration once", async () => {
			const config = { mode: "provider", thirdPartySignIn: true, authority: "https://x" }
			fetchMock.mockResolvedValue(reply(true, config))
			expect(await api.loadAuthConfig()).toEqual(config)
			expect(await api.loadAuthConfig()).toEqual(config)
			expect(fetchMock).toHaveBeenCalledTimes(1)
		})

		it("falls back to the narrow option on a non-ok response and retries later", async () => {
			fetchMock.mockResolvedValueOnce(reply(false))
			expect(await api.loadAuthConfig()).toEqual({ mode: "development", thirdPartySignIn: false, authority: null })
			const config = { mode: "provider", thirdPartySignIn: true, authority: null }
			fetchMock.mockResolvedValueOnce(reply(true, config))
			expect(await api.loadAuthConfig()).toEqual(config)
		})

		it("falls back when the request fails", async () => {
			fetchMock.mockRejectedValue(new Error("down"))
			expect((await api.loadAuthConfig()).thirdPartySignIn).toBe(false)
		})
	})

	describe("requestToken", () => {
		it("returns the session on success", async () => {
			const token = { accessToken: "a", expiresAt: "e", subject: "s", role: "user" }
			fetchMock.mockResolvedValue(reply(true, token))
			expect(await api.requestToken("u", "p")).toEqual(token)
			expect(fetchMock).toHaveBeenCalledWith("/api/auth/token", expect.objectContaining({ method: "POST", body: JSON.stringify({ username: "u", password: "p" }) }))
		})

		it("throws a sign-in error when refused", async () => {
			fetchMock.mockResolvedValue(reply(false))
			const failure = await api.requestToken("u", "p").catch((error: unknown) => error)
			expect(failure).toBeInstanceOf(api.SignInError)
			expect((failure as Error).name).toBe("SignInError")
			expect((failure as Error).message).toBe("sign-in-failed")
		})
	})

	describe("fetchIdentity", () => {
		it("returns who the token names", async () => {
			fetchMock.mockResolvedValue(reply(true, { subject: "s", role: "administrator" }))
			expect(await api.fetchIdentity("tok")).toEqual({ subject: "s", role: "administrator" })
			expect(fetchMock).toHaveBeenCalledWith("/api/auth/me", { headers: { Authorization: "Bearer tok" } })
		})

		it("returns null when the token is not valid", async () => {
			fetchMock.mockResolvedValue(reply(false))
			expect(await api.fetchIdentity("tok")).toBeNull()
		})
	})
})
