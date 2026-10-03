import { act, cleanup, render, renderHook, screen, waitFor } from "@testing-library/react"
import type { FormEvent, ReactNode } from "react"
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { AuthContext, type AuthContextValue } from "../auth/AuthContext"
import { MemberLoginPage, useMemberLoginPage } from "./MemberLoginPage"

const loadAuthConfig = vi.hoisted(() => vi.fn())
vi.mock("../auth/authApi", () => ({ loadAuthConfig }))
vi.mock("./MemberLoginPage.view", () => ({
	MemberLoginPageView: ({ username }: { username: string }) => <p>login view {username}</p>,
}))

const signInWithPassword = vi.fn<(username: string, password: string) => Promise<void>>()

function wrapperAt(url: string) {
	const value: AuthContextValue = { status: "signedOut", isSignedIn: false, role: null, signInWithPassword, signOut: () => {} }
	return ({ children }: { children: ReactNode }) => (
		<AuthContext.Provider value={value}>
			<MemoryRouter initialEntries={[url]}>
				<Routes>
					<Route path="/login" element={children} />
					<Route path="*" element={<Where />} />
				</Routes>
			</MemoryRouter>
		</AuthContext.Provider>
	)
}

function Where() {
	const location = useLocation()
	return <p>{`at ${location.pathname}`}</p>
}

const preventDefault = vi.fn()
const submit = { preventDefault } as unknown as FormEvent

afterEach(cleanup)

beforeEach(() => {
	vi.clearAllMocks()
	loadAuthConfig.mockResolvedValue({ thirdPartySignIn: true })
	signInWithPassword.mockResolvedValue(undefined)
})

describe("useMemberLoginPage", () => {
	it("starts empty and learns from the API whether a third-party sign-in is offered", async () => {
		const { result } = renderHook(() => useMemberLoginPage(), { wrapper: wrapperAt("/login") })

		expect(result.current).toMatchObject({ username: "", password: "", submitting: false, failed: false, thirdPartySignIn: false })
		await waitFor(() => expect(result.current.thirdPartySignIn).toBe(true))
	})

	it("ignores the configuration when it arrives after the page has gone", async () => {
		let resolve: (config: { thirdPartySignIn: boolean }) => void = () => {}
		loadAuthConfig.mockReturnValue(new Promise((done) => (resolve = done)))
		const { result, unmount } = renderHook(() => useMemberLoginPage(), { wrapper: wrapperAt("/login") })

		unmount()
		await act(async () => {
			resolve({ thirdPartySignIn: true })
			await Promise.resolve()
		})

		expect(result.current.thirdPartySignIn).toBe(false)
	})

	it("signs in with the typed credentials and goes to the requested path", async () => {
		const { result } = renderHook(() => useMemberLoginPage(), { wrapper: wrapperAt("/login?returnTo=%2Freports%2F1") })
		await waitFor(() => expect(result.current.thirdPartySignIn).toBe(true))

		act(() => {
			result.current.onUsernameChange("pilot@example.com")
			result.current.onPasswordChange("secret")
		})
		await act(async () => result.current.onSubmit(submit))

		expect(signInWithPassword).toHaveBeenCalledWith("pilot@example.com", "secret")
		expect(preventDefault).toHaveBeenCalled()
	})

	it("reports one failure for any refusal and stops submitting", async () => {
		signInWithPassword.mockRejectedValue(new Error("no"))
		const { result } = renderHook(() => useMemberLoginPage(), { wrapper: wrapperAt("/login") })
		await waitFor(() => expect(result.current.thirdPartySignIn).toBe(true))

		await act(async () => result.current.onSubmit(submit))

		expect(result.current.failed).toBe(true)
		expect(result.current.submitting).toBe(false)
	})
})

describe("MemberLoginPage", () => {
	it("renders its view with the view model", async () => {
		render(<MemberLoginPage />, { wrapper: wrapperAt("/login") })

		expect(screen.getByText("login view")).toBeTruthy()
		await waitFor(() => expect(loadAuthConfig).toHaveBeenCalled())
	})
})

describe("MemberLoginPage navigation", () => {
	it("lands on the requested path", async () => {
		function Probe() {
			const page = useMemberLoginPage()
			return <button onClick={(event) => void page.onSubmit(event)}>go</button>
		}
		render(<Probe />, { wrapper: wrapperAt("/login?returnTo=%2Freports%2F1") })

		await act(async () => {
			screen.getByText("go").click()
			await Promise.resolve()
		})

		await waitFor(() => expect(screen.getByText("at /reports/1")).toBeTruthy())
	})
})
