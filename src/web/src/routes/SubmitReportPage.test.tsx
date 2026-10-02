import { render, renderHook, screen } from "@testing-library/react"
import type { ReactNode } from "react"
import { describe, expect, it, vi } from "vitest"
import { AuthContext, type AuthContextValue } from "../auth/AuthContext"
import { SubmitReportPage, useSubmitReportPage } from "./SubmitReportPage"

vi.mock("./SubmitReportPage.view", () => ({
	SubmitReportPageView: ({ status, isSignedIn }: { status: string; isSignedIn: boolean }) => <p>{`${status} ${isSignedIn}`}</p>,
}))

function auth(value: Partial<AuthContextValue>) {
	const full: AuthContextValue = { status: "signedOut", isSignedIn: false, role: null, signInWithPassword: async () => {}, signOut: () => {}, ...value }
	return ({ children }: { children: ReactNode }) => <AuthContext.Provider value={full}>{children}</AuthContext.Provider>
}

describe("useSubmitReportPage", () => {
	it("exposes the authentication status and whether the member is signed in", () => {
		const { result } = renderHook(() => useSubmitReportPage(), { wrapper: auth({ status: "signedIn", isSignedIn: true }) })

		expect(result.current).toEqual({ status: "signedIn", isSignedIn: true })
	})
})

describe("SubmitReportPage", () => {
	it("renders its view with the authentication state", () => {
		render(<SubmitReportPage />, { wrapper: auth({ status: "unknown" }) })

		expect(screen.getByText("unknown false")).toBeTruthy()
	})
})
