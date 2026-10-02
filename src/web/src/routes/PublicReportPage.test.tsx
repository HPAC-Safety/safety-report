import { act, cleanup, render, renderHook, screen, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { MemoryRouter, Route, Routes } from "react-router-dom"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { AuthContext, type AuthContextValue } from "../auth/AuthContext"
import { LocaleContext } from "../i18n/LocaleProvider"
import { PublicReportPage, usePublicReportPage } from "./PublicReportPage"

const fetchPublicReport = vi.hoisted(() => vi.fn())
vi.mock("../api/publicReports", () => {
	class PublicReportNotFound extends Error {}
	return {
		PublicReportNotFound,
		fetchPublicReport,
		summaryIn: (report: { aiSummaryEn: string; aiSummaryFr: string }, locale: string) => (locale === "fr-CA" ? report.aiSummaryFr : report.aiSummaryEn),
	}
})
vi.mock("./PublicReportPage.view", () => ({
	PublicReportPageView: ({ loaded }: { loaded: { state: string } }) => <p>{`view ${loaded.state}`}</p>,
}))

import { PublicReportNotFound } from "../api/publicReports"

const report = { id: "r1", aiSummaryEn: "English", aiSummaryFr: "Francais", publishedAt: "2026-01-02T00:00:00Z", language: "en-CA", media: [], staffAttachments: null }

function wrapper({ locale = "en-CA", signedIn = false, role = null as AuthContextValue["role"], path = "/reports/r1" } = {}) {
	const auth: AuthContextValue = { status: "signedIn", isSignedIn: signedIn, role, signInWithPassword: async () => {}, signOut: () => {} }
	return ({ children }: { children: ReactNode }) => (
		<LocaleContext.Provider value={{ locale: locale as "en-CA", setLocale: () => {}, t: (key) => key }}>
			<AuthContext.Provider value={auth}>
				<MemoryRouter initialEntries={[path]}>
					<Routes>
						<Route path="/reports/:reportId" element={children} />
						<Route path="*" element={children} />
					</Routes>
				</MemoryRouter>
			</AuthContext.Provider>
		</LocaleContext.Provider>
	)
}

afterEach(cleanup)

beforeEach(() => {
	vi.clearAllMocks()
	fetchPublicReport.mockResolvedValue(report)
})

describe("usePublicReportPage", () => {
	it("loads the report named in the address and gives its summary in the site's language", async () => {
		const { result } = renderHook(() => usePublicReportPage(), { wrapper: wrapper() })

		expect(result.current.loaded).toEqual({ state: "loading" })
		await waitFor(() => expect(result.current.loaded).toEqual({ state: "ready", report, summary: "English" }))
		expect(fetchPublicReport).toHaveBeenCalledWith("r1")
	})

	it("gives the French summary under the French locale", async () => {
		const { result } = renderHook(() => usePublicReportPage(), { wrapper: wrapper({ locale: "fr-CA" }) })

		await waitFor(() => expect(result.current.loaded).toMatchObject({ state: "ready", summary: "Francais" }))
	})

	it("treats a report that is not public as missing", async () => {
		fetchPublicReport.mockRejectedValue(new PublicReportNotFound())
		const { result } = renderHook(() => usePublicReportPage(), { wrapper: wrapper() })

		await waitFor(() => expect(result.current.loaded).toEqual({ state: "missing" }))
	})

	it("reports any other failure as failed", async () => {
		fetchPublicReport.mockRejectedValue(new Error("boom"))
		const { result } = renderHook(() => usePublicReportPage(), { wrapper: wrapper() })

		await waitFor(() => expect(result.current.loaded).toEqual({ state: "failed" }))
	})

	it("asks for an empty id when the address names none", async () => {
		renderHook(() => usePublicReportPage(), { wrapper: wrapper({ path: "/elsewhere" }) })

		await waitFor(() => expect(fetchPublicReport).toHaveBeenCalledWith(""))
	})

	it("reloads without showing the loading state when told a change happened", async () => {
		const { result } = renderHook(() => usePublicReportPage(), { wrapper: wrapper() })
		await waitFor(() => expect(result.current.loaded.state).toBe("ready"))

		act(() => result.current.onChanged())

		expect(result.current.loaded.state).toBe("ready")
		await waitFor(() => expect(fetchPublicReport).toHaveBeenCalledTimes(2))
	})

	it("marks a signed-in Safety Officer as a reviewer, and a plain member or visitor as not", async () => {
		const officer = renderHook(() => usePublicReportPage(), { wrapper: wrapper({ signedIn: true, role: "safetyOfficer" as AuthContextValue["role"] }) })
		const member = renderHook(() => usePublicReportPage(), { wrapper: wrapper({ signedIn: true, role: "user" }) })
		const visitor = renderHook(() => usePublicReportPage(), { wrapper: wrapper() })

		expect([officer.result.current.isReviewer, member.result.current.isReviewer, visitor.result.current.isReviewer]).toEqual([true, false, false])
		await waitFor(() => expect(officer.result.current.loaded.state).toBe("ready"))
	})
})

describe("PublicReportPage", () => {
	it("renders its view with the view model", async () => {
		render(<PublicReportPage />, { wrapper: wrapper() })

		expect(screen.getByText("view loading")).toBeTruthy()
		await waitFor(() => expect(screen.getByText("view ready")).toBeTruthy())
	})
})
