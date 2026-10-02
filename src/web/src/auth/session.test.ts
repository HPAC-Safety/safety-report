import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { clearSession, readSession, writeSession, type MemberSession } from "./session"

const session: MemberSession = { accessToken: "tok", expiresAt: "2030-01-01T00:00:00Z", subject: "sub", role: "user" }

describe("session storage", () => {
	beforeEach(() => sessionStorage.clear())
	afterEach(() => vi.restoreAllMocks())

	it("reads nothing when nothing is stored", () => {
		expect(readSession()).toBeNull()
	})

	it("round-trips a written session", () => {
		writeSession(session)
		expect(readSession()).toEqual(session)
	})

	it("clears a stored session", () => {
		writeSession(session)
		clearSession()
		expect(readSession()).toBeNull()
	})

	it("treats malformed JSON as signed out", () => {
		sessionStorage.setItem("hpac.session", "{nope")
		expect(readSession()).toBeNull()
	})

	it.each([
		["an empty token", { ...session, accessToken: "" }],
		["a missing token", { ...session, accessToken: undefined }],
		["a missing subject", { ...session, subject: undefined }],
		["a missing expiry", { ...session, expiresAt: undefined }],
		["an unknown role", { ...session, role: "root" }],
	])("rejects %s", (_name, value) => {
		sessionStorage.setItem("hpac.session", JSON.stringify(value))
		expect(readSession()).toBeNull()
	})

	it("survives unavailable storage on every operation", () => {
		vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
			throw new Error("blocked")
		})
		vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
			throw new Error("blocked")
		})
		vi.spyOn(Storage.prototype, "removeItem").mockImplementation(() => {
			throw new Error("blocked")
		})
		expect(readSession()).toBeNull()
		expect(() => writeSession(session)).not.toThrow()
		expect(() => clearSession()).not.toThrow()
	})
})
