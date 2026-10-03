import { act, cleanup, render, renderHook, screen } from "@testing-library/react"
import type { ReactNode } from "react"
import { MemoryRouter, useLocation } from "react-router-dom"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { LocaleContext } from "../i18n/LocaleProvider"
import { useViewReportsPage, ViewReportsPage } from "./ViewReportsPage"

const fetchPublicReports = vi.hoisted(() => vi.fn())
const fetchOwnReports = vi.hoisted(() => vi.fn())
const useInfiniteReportList = vi.hoisted(() =>
	vi.fn<(options: { storageKey: string; getId: (item: { id: string }) => string; fetchPage: (after: string | null) => Promise<unknown> }) => unknown>(),
)
vi.mock("../api/publicReports", () => ({
	fetchPublicReports,
	summaryIn: (report: { aiSummaryEn: string; aiSummaryFr: string }, locale: string) => (locale === "fr-CA" ? report.aiSummaryFr : report.aiSummaryEn),
}))
vi.mock("../api/ownReports", () => ({
	fetchOwnReports,
	ownSummaryIn: (report: { aiSummaryEn: string | null; aiSummaryFr: string | null }, locale: string) =>
		locale === "fr-CA" ? report.aiSummaryFr : report.aiSummaryEn,
}))
vi.mock("../hooks/useInfiniteReportList", () => ({ useInfiniteReportList }))
vi.mock("./ViewReportsPage.view", () => ({ ViewReportsPageView: ({ rows }: { rows: unknown[] }) => <p>{`view ${rows.length}`}</p> }))

const sentinelRef = () => {}
const listed = {
	items: [{ id: "a", aiSummaryEn: "## Title\nEnglish body", aiSummaryFr: "## Titre\nCorps", publishedAt: "2026-03-04T12:00:00Z", commentCount: 2, attachmentCount: 1 }],
	initialLoading: false,
	loadingMore: false,
	failed: false,
	hasMore: true,
	loadMore: () => {},
	sentinelRef,
	announcement: "",
}

let here = ""
function Where() {
	const location = useLocation()
	here = location.search
	return null
}

function wrapper(locale: string, url = "/reports") {
	return ({ children }: { children: ReactNode }) => (
		<LocaleContext.Provider value={{ locale: locale as "en-CA", setLocale: () => {}, t: (key) => key }}>
			<MemoryRouter initialEntries={[url]}>
				<Where />
				{children}
			</MemoryRouter>
		</LocaleContext.Provider>
	)
}

afterEach(cleanup)

beforeEach(() => {
	vi.useFakeTimers()
	vi.clearAllMocks()
	useInfiniteReportList.mockReturnValue(listed)
	fetchOwnReports.mockResolvedValue([])
})

afterEach(() => vi.useRealTimers())

/** Lets the mocked lookup resolve; the timers here are fake, so waitFor cannot poll. */
async function settle() {
	await act(async () => {
		await Promise.resolve()
	})
}

describe("useViewReportsPage: the visitor's own reports (issue no. 820)", () => {
	const own = [
		{ id: "o1", submittedAt: "2026-03-05T12:00:00Z", forPublication: true, aiSummaryEn: "## Title\nDraft body", aiSummaryFr: "## Titre\nBrouillon", attachmentCount: 2 },
		{ id: "o2", submittedAt: "2026-03-04T12:00:00Z", forPublication: true, aiSummaryEn: null, aiSummaryFr: null, attachmentCount: 0 },
		{ id: "o3", submittedAt: "2026-03-03T12:00:00Z", forPublication: false, aiSummaryEn: null, aiSummaryFr: null, attachmentCount: 0 },
	]

	it("lists them above the feed with their submitted date, and a preview only where there is a summary", async () => {
		fetchOwnReports.mockResolvedValue(own)
		const { result } = renderHook(() => useViewReportsPage(), { wrapper: wrapper("en-CA") })

		await settle()
		expect(result.current.ownRows).toHaveLength(3)
		expect(result.current.ownRows.map((row) => row.id)).toEqual(["o1", "o2", "o3"])
		expect(result.current.ownRows[0]).toMatchObject({ submitted: "March 5, 2026", forPublication: true, attachmentCount: 2 })
		expect(result.current.ownRows[0].preview).toContain("Draft body")
		expect(result.current.ownRows[1].preview).toBeNull()
		expect(result.current.ownRows[2]).toMatchObject({ forPublication: false, preview: null })
	})

	it("reads the French draft under the French locale", async () => {
		fetchOwnReports.mockResolvedValue(own)
		const { result } = renderHook(() => useViewReportsPage(), { wrapper: wrapper("fr-CA") })

		await settle()
		expect(result.current.ownRows[0].preview).toContain("Brouillon")
	})

	it("shows none, and asks for none, while a search is active", async () => {
		fetchOwnReports.mockResolvedValue(own)
		const { result } = renderHook(() => useViewReportsPage(), { wrapper: wrapper("en-CA", "/reports?q=wing") })

		await settle()
		expect(result.current.ownRows).toEqual([])
		expect(fetchOwnReports).not.toHaveBeenCalled()
	})
})

describe("useViewReportsPage", () => {
	it("turns each report into a row with its date and preview in the site's language", () => {
		const { result } = renderHook(() => useViewReportsPage(), { wrapper: wrapper("en-CA") })

		expect(result.current.rows).toEqual([{ id: "a", published: "March 4, 2026", preview: expect.any(String) as string, commentCount: 2, attachmentCount: 1 }])
		expect(result.current.rows[0].preview).toContain("English body")
	})

	it("reads the French summary under the French locale", () => {
		const { result } = renderHook(() => useViewReportsPage(), { wrapper: wrapper("fr-CA") })

		expect(result.current.rows[0].preview).toContain("Corps")
	})

	it("passes the list through, and the search term from the address", () => {
		const { result } = renderHook(() => useViewReportsPage(), { wrapper: wrapper("en-CA", "/reports?q=wing") })

		expect(result.current).toMatchObject({ q: "wing", searchBox: "wing", loading: false, loadingMore: false, failed: false, hasMore: true, sentinelRef, announcement: "" })
	})

	it("keys the list by the search term and fetches pages with it", () => {
		renderHook(() => useViewReportsPage(), { wrapper: wrapper("en-CA", "/reports?q=wing") })
		const options = useInfiniteReportList.mock.calls[0][0]
		void options.fetchPage("cursor")

		expect(options.storageKey).toBe("public:wing")
		expect(options.getId({ id: "z" })).toBe("z")
		expect(fetchPublicReports).toHaveBeenCalledWith("cursor", "wing", "en-CA")
	})

	it("puts a settled search in the address after the debounce, once, for the last text typed", () => {
		const { result } = renderHook(() => useViewReportsPage(), { wrapper: wrapper("en-CA") })

		act(() => result.current.onSearchBoxChange("w"))
		act(() => result.current.onSearchBoxChange("wing"))
		expect(result.current.searchBox).toBe("wing")
		expect(here).toBe("")

		void act(() => vi.advanceTimersByTime(300))

		expect(here).toBe("?q=wing")
		expect(result.current.q).toBe("wing")
	})

	it("clears the search from the address when the box is emptied", () => {
		const { result } = renderHook(() => useViewReportsPage(), { wrapper: wrapper("en-CA", "/reports?q=wing") })

		act(() => result.current.onSearchBoxChange(""))
		void act(() => vi.advanceTimersByTime(300))

		expect(here).toBe("")
	})

	it("drops a pending search when the page goes away", () => {
		const { result, unmount } = renderHook(() => useViewReportsPage(), { wrapper: wrapper("en-CA") })

		act(() => result.current.onSearchBoxChange("wing"))
		unmount()
		void act(() => vi.advanceTimersByTime(300))

		expect(here).toBe("")
	})

	it("leaves nothing to clear when it goes away without a pending search", () => {
		const { unmount } = renderHook(() => useViewReportsPage(), { wrapper: wrapper("en-CA") })

		expect(() => unmount()).not.toThrow()
	})
})

describe("ViewReportsPage", () => {
	it("renders its view with the rows", () => {
		render(<ViewReportsPage />, { wrapper: wrapper("en-CA") })

		expect(screen.getByText("view 1")).toBeTruthy()
	})
})
