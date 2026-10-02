import { cleanup, renderHook } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { AuthContext, type AuthContextValue } from "./AuthContext"
import { useAuth } from "./useAuth"

describe("useAuth", () => {
	afterEach(cleanup)

	it("returns the provided context value", () => {
		const value: AuthContextValue = {
			status: "signedOut",
			isSignedIn: false,
			role: null,
			signInWithPassword: vi.fn(),
			signOut: vi.fn(),
		}
		const wrapper = ({ children }: { children: ReactNode }) => <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
		const { result } = renderHook(() => useAuth(), { wrapper })
		expect(result.current).toBe(value)
	})

	it("throws outside a provider", () => {
		const error = vi.spyOn(console, "error").mockImplementation(() => {})
		expect(() => renderHook(() => useAuth())).toThrow("useAuth must be used within an AuthProvider")
		error.mockRestore()
	})
})
