import { act, cleanup, render, renderHook, screen, waitFor } from "@testing-library/react"
import { useContext, type ReactNode } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { AuthContext, AuthProvider, useAuthProvider } from "./AuthContext"
import type { MemberSession } from "./session"

const fetchIdentity = vi.fn<(token: string) => Promise<unknown>>()
const requestToken = vi.fn<(username: string, password: string) => Promise<unknown>>()
vi.mock("./authApi", () => ({
	fetchIdentity: (token: string) => fetchIdentity(token),
	requestToken: (username: string, password: string) => requestToken(username, password),
}))

const readSession = vi.fn<() => MemberSession | null>()
const writeSession = vi.fn<(session: MemberSession) => void>()
const clearSession = vi.fn<() => void>()
vi.mock("./session", () => ({
	readSession: () => readSession(),
	writeSession: (session: MemberSession) => writeSession(session),
	clearSession: () => clearSession(),
}))

const stored: MemberSession = { accessToken: "token", expiresAt: "2030-01-01T00:00:00Z", subject: "old", role: "user" }

beforeEach(() => {
	readSession.mockReturnValue(null)
})

afterEach(() => {
	cleanup()
	for (const mock of [fetchIdentity, requestToken, readSession, writeSession, clearSession]) mock.mockReset()
})

describe("useAuthProvider", () => {
	it("is signed out when nothing is stored", async () => {
		const { result } = renderHook(() => useAuthProvider())

		await waitFor(() => expect(result.current.status).toBe("signedOut"))
		expect(result.current.isSignedIn).toBe(false)
		expect(result.current.role).toBeNull()
		expect(fetchIdentity).not.toHaveBeenCalled()
	})

	it("checks a stored session against the API and takes the role it reports", async () => {
		readSession.mockReturnValue(stored)
		fetchIdentity.mockResolvedValue({ subject: "new", role: "administrator" })

		const { result } = renderHook(() => useAuthProvider())

		expect(result.current.status).toBe("unknown")
		await waitFor(() => expect(result.current.status).toBe("signedIn"))
		expect(fetchIdentity).toHaveBeenCalledWith("token")
		expect(result.current.isSignedIn).toBe(true)
		expect(result.current.role).toBe("administrator")
		expect(writeSession).toHaveBeenCalledWith({ ...stored, subject: "new", role: "administrator" })
	})

	it("drops a stored session the API does not recognise", async () => {
		readSession.mockReturnValue(stored)
		fetchIdentity.mockResolvedValue(null)

		const { result } = renderHook(() => useAuthProvider())

		await waitFor(() => expect(result.current.status).toBe("signedOut"))
		expect(clearSession).toHaveBeenCalledTimes(1)
	})

	it("drops a stored session when the API cannot be reached", async () => {
		readSession.mockReturnValue(stored)
		fetchIdentity.mockRejectedValue(new Error("offline"))

		const { result } = renderHook(() => useAuthProvider())

		await waitFor(() => expect(result.current.status).toBe("signedOut"))
		expect(clearSession).toHaveBeenCalledTimes(1)
	})

	it("ignores an answer that arrives after it unmounted", async () => {
		readSession.mockReturnValue(stored)
		let resolve: (identity: unknown) => void = () => {}
		fetchIdentity.mockReturnValue(new Promise((done) => (resolve = done)))
		const { unmount } = renderHook(() => useAuthProvider())

		unmount()
		await act(async () => {
			resolve({ subject: "new", role: "administrator" })
			await Promise.resolve()
		})

		expect(writeSession).not.toHaveBeenCalled()
	})

	it("ignores a failure that arrives after it unmounted", async () => {
		readSession.mockReturnValue(stored)
		let reject: (error: Error) => void = () => {}
		fetchIdentity.mockReturnValue(new Promise((_, fail) => (reject = fail)))
		const { unmount } = renderHook(() => useAuthProvider())

		unmount()
		await act(async () => {
			reject(new Error("offline"))
			await Promise.resolve()
		})

		expect(clearSession).not.toHaveBeenCalled()
	})

	it("signs in with a password and keeps the issued session", async () => {
		const issued: MemberSession = { ...stored, role: "safety_officer" }
		requestToken.mockResolvedValue(issued)
		const { result } = renderHook(() => useAuthProvider())
		await waitFor(() => expect(result.current.status).toBe("signedOut"))

		await act(() => result.current.signInWithPassword("me", "secret"))

		expect(requestToken).toHaveBeenCalledWith("me", "secret")
		expect(writeSession).toHaveBeenCalledWith(issued)
		expect(result.current.status).toBe("signedIn")
		expect(result.current.role).toBe("safety_officer")
	})

	it("signs out", async () => {
		requestToken.mockResolvedValue(stored)
		const { result } = renderHook(() => useAuthProvider())
		await act(() => result.current.signInWithPassword("me", "secret"))

		act(() => result.current.signOut())

		expect(clearSession).toHaveBeenCalledTimes(1)
		expect(result.current.status).toBe("signedOut")
		expect(result.current.role).toBeNull()
	})
})

describe("AuthProvider", () => {
	function Reader(): ReactNode {
		const value = useContext(AuthContext)
		return <p>{value?.status}</p>
	}

	it("provides the context to its children", async () => {
		render(
			<AuthProvider>
				<Reader />
			</AuthProvider>,
		)

		await waitFor(() => expect(screen.getByText("signedOut")).toBeTruthy())
	})
})
