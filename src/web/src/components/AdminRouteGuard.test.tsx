import { cleanup, render, renderHook, screen } from "@testing-library/react"
import type { ReactNode } from "react"
import { MemoryRouter, Route, Routes } from "react-router-dom"
import { afterEach, describe, expect, it, vi } from "vitest"
import { AuthContext, type AuthContextValue, type AuthStatus } from "../auth/AuthContext"
import type { MemberRole } from "../auth/session"
import { AdminRouteGuard, useAdminRouteGuard } from "./AdminRouteGuard"

vi.mock("../routes/ForbiddenPage", () => ({ ForbiddenPage: () => <p>forbidden</p> }))

afterEach(cleanup)

function authWrapper(status: AuthStatus, role: MemberRole | null) {
	const value: AuthContextValue = {
		status,
		isSignedIn: status === "signedIn",
		role,
		signInWithPassword: vi.fn(),
		signOut: vi.fn(),
	}
	return ({ children }: { children: ReactNode }) => <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

function outcome(requires: "reviewer" | "administrator", status: AuthStatus, role: MemberRole | null) {
	return renderHook(() => useAdminRouteGuard({ requires, children: null }), { wrapper: authWrapper(status, role) }).result.current.outcome
}

describe("useAdminRouteGuard", () => {
	it("waits while the stored session is still being checked", () => {
		expect(outcome("reviewer", "unknown", null)).toBe("checking")
	})

	it("sends a signed-out visitor to sign in", () => {
		expect(outcome("reviewer", "signedOut", null)).toBe("signedOut")
	})

	it("lets an administrator into an administrator route", () => {
		expect(outcome("administrator", "signedIn", "administrator")).toBe("allowed")
	})

	it("keeps a safety officer out of an administrator route", () => {
		expect(outcome("administrator", "signedIn", "safety_officer")).toBe("forbidden")
	})

	it("lets an administrator into a reviewer route", () => {
		expect(outcome("reviewer", "signedIn", "administrator")).toBe("allowed")
	})

	it("lets a safety officer into a reviewer route", () => {
		expect(outcome("reviewer", "signedIn", "safety_officer")).toBe("allowed")
	})

	it("keeps a plain member out of a reviewer route", () => {
		expect(outcome("reviewer", "signedIn", "user")).toBe("forbidden")
	})
})

describe("AdminRouteGuard", () => {
	function renderGuard(status: AuthStatus, role: MemberRole | null) {
		const Wrapper = authWrapper(status, role)
		return render(
			<Wrapper>
				<MemoryRouter initialEntries={["/admin"]}>
					<Routes>
						<Route path="/admin" element={<AdminRouteGuard requires="reviewer"><p>admin content</p></AdminRouteGuard>} />
						<Route path="/login" element={<p>login page</p>} />
					</Routes>
				</MemoryRouter>
			</Wrapper>,
		)
	}

	it("draws nothing while checking", () => {
		const { container } = renderGuard("unknown", null)

		expect(container.innerHTML).toBe("")
	})

	it("redirects a signed-out visitor to the login page", () => {
		renderGuard("signedOut", null)

		expect(screen.getByText("login page")).toBeTruthy()
	})

	it("shows the forbidden view to a member who may not enter", () => {
		renderGuard("signedIn", "user")

		expect(screen.getByText("forbidden")).toBeTruthy()
	})

	it("shows the route to a member who may", () => {
		renderGuard("signedIn", "safety_officer")

		expect(screen.getByText("admin content")).toBeTruthy()
	})
})
