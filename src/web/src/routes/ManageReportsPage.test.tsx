import { act, render, renderHook, screen } from "@testing-library/react"
import type { ReactNode } from "react"
import { MemoryRouter, useLocation } from "react-router-dom"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"

const api = vi.hoisted(() => ({
	deleteReport: vi.fn(),
	listReports: vi.fn(),
	publishReport: vi.fn(),
	unpublishReport: vi.fn(),
}))
const list = vi.hoisted((): { options: unknown; mutate: ReturnType<typeof vi.fn>; reload: ReturnType<typeof vi.fn> } => ({ options: undefined, mutate: vi.fn(), reload: vi.fn() }))
const t = vi.hoisted(() => (key: string) => key)
vi.mock("../i18n/useLocale", () => ({ useLocale: () => ({ locale: "en-CA", t }) }))
vi.mock("../api/adminReports", async (original) => ({ ...(await original<typeof import("../api/adminReports")>()), ...api }))
vi.mock("../api/adminQuestions", () => ({
	ApiError: class ApiError extends Error {
		constructor(
			public type: string,
			public detail: string,
		) {
			super(detail)
		}
	},
}))
vi.mock("../hooks/useInfiniteReportList", () => ({
	useInfiniteReportList: (options: unknown) => {
		list.options = options
		return {
			items: [],
			initialLoading: false,
			loadingMore: false,
			failed: false,
			hasMore: false,
			loadMore: vi.fn(),
			sentinelRef: vi.fn(),
			announcement: "",
			mutate: list.mutate,
			reload: list.reload,
		}
	},
}))
vi.mock("./ManageReportsPage.view", () => ({
	ManageReportsPageView: (props: { filter: string }) => <p data-testid="view">{props.filter}</p>,
}))

import { ApiError } from "../api/adminQuestions"
import { STALE_REPORT } from "../api/adminReports"
import { ManageReportsPage, useManageReportsPage, type ReportListItem } from "./ManageReportsPage"

let search = ""
function Probe() {
	search = useLocation().search
	return null
}
const at = (url: string) => ({ children }: { children: ReactNode }) => (
	<MemoryRouter initialEntries={[url]}>
		<Probe />
		{children}
	</MemoryRouter>
)
const row = (id: string, over: Partial<ReportListItem> = {}) => ({ id, version: "v" + id, status: "pending", consent: true, isStuck: false, ...over }) as ReportListItem
const failure = (type: string, detail: string) => new (ApiError as unknown as new (type: string, detail: string) => Error)(type, detail)
const rows = () => (list.mutate.mock.calls.at(-1)![0] as (current: ReportListItem[]) => ReportListItem[])([row("1"), row("2")])

describe("useManageReportsPage", () => {
	beforeEach(() => {
		for (const fn of [...Object.values(api), list.mutate, list.reload]) fn.mockReset()
	})

	it("lists every report unless the address names a filter", () => {
		expect(renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports") }).result.current.filter).toBe("all")
		expect(renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports?filter=published") }).result.current.filter).toBe("published")
		expect(renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports?filter=nope") }).result.current.filter).toBe("all")
	})

	it("fetches a page of the filtered, searched list, keyed by both", async () => {
		api.listReports.mockResolvedValue({ items: [], next: null })
		const { result } = renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports?filter=private&q=abc") })
		const options = list.options as { storageKey: string; getId: (report: ReportListItem) => string; fetchPage: (after: string | null) => Promise<unknown> }

		await options.fetchPage("cursor")

		expect(result.current).toMatchObject({ q: "abc", searchInput: "abc", filters: expect.arrayContaining(["all", "private"]) as string[] })
		expect(options.storageKey).toBe("admin:private:abc")
		expect(options.getId(row("7"))).toBe("7")
		expect(api.listReports).toHaveBeenCalledWith("private", "cursor", "abc")
	})

	it("formats a submission date in the reader's locale", () => {
		const { result } = renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports") })

		expect(result.current.formatSubmitted("2026-01-02T03:04:05Z")).toContain("2026")
	})

	describe("search", () => {
		beforeEach(() => {
			vi.useFakeTimers()
		})
		afterEach(() => {
			vi.useRealTimers()
		})

		it("puts a typed term in the address bar after a pause, trimmed", () => {
			const { result } = renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports?filter=private") })

			act(() => result.current.changeSearch("  abc  "))
			expect(result.current.searchInput).toBe("  abc  ")
			expect(search).toBe("?filter=private")
			act(() => void vi.advanceTimersByTime(300))

			expect(search).toBe("?filter=private&q=abc")
			expect(result.current.q).toBe("abc")
		})

		it("waits for the typing to stop", () => {
			const { result } = renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports") })

			act(() => result.current.changeSearch("a"))
			act(() => void vi.advanceTimersByTime(200))
			act(() => result.current.changeSearch("ab"))
			act(() => void vi.advanceTimersByTime(200))
			expect(search).toBe("")
			act(() => void vi.advanceTimersByTime(100))

			expect(search).toBe("?q=ab")
		})

		it("takes the term out of the address bar when it is cleared", () => {
			const { result } = renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports?q=abc") })

			act(() => result.current.clearSearch())
			act(() => void vi.advanceTimersByTime(300))

			expect(search).toBe("")
			expect(result.current.q).toBe("")
		})

		it("does nothing when the typed term is the one already in the address", () => {
			const { result } = renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports?q=abc") })

			act(() => result.current.changeSearch("abc "))
			act(() => void vi.advanceTimersByTime(300))

			expect(search).toBe("?q=abc")
		})
	})

	describe("row actions", () => {
		it("asks before deleting", () => {
			const { result } = renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports") })

			act(() => result.current.act(row("1"), "delete"))
			expect(result.current.confirmingDelete).toBe("1")
			act(() => result.current.keepReport())

			expect(result.current.confirmingDelete).toBeNull()
			expect(api.deleteReport).not.toHaveBeenCalled()
		})

		it("publishes in place with the row's version and updates only that row", async () => {
			api.publishReport.mockResolvedValue({ id: "1", status: "published", consent: true, isStuck: false, version: "v9" })
			const { result } = renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports") })

			await act(async () => result.current.act(row("1"), "publish"))

			expect(api.publishReport).toHaveBeenCalledWith("1", "v1")
			expect(rows()).toEqual([{ ...row("1"), status: "published", version: "v9" }, row("2")])
			expect(result.current.busyId).toBeNull()
		})

		it("unpublishes in place without a note", async () => {
			api.unpublishReport.mockResolvedValue({ id: "2", status: "unpublished", consent: true, isStuck: false, version: "v8" })
			const { result } = renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports") })

			await act(async () => result.current.act(row("2"), "unpublish"))

			expect(api.unpublishReport).toHaveBeenCalledWith("2", "v2", "")
			expect(rows()[1]).toMatchObject({ status: "unpublished", version: "v8" })
		})

		it("marks the row busy while a command runs", async () => {
			let finish: (value: unknown) => void = () => {}
			api.publishReport.mockReturnValue(new Promise((resolve) => (finish = resolve)))
			const { result } = renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports") })

			act(() => result.current.act(row("1"), "publish"))
			expect(result.current.busyId).toBe("1")
			await act(async () => finish({ id: "1", status: "published", consent: true, isStuck: false, version: "v9" }))

			expect(result.current.busyId).toBeNull()
		})

		it("deletes after confirmation and drops the row", async () => {
			api.deleteReport.mockResolvedValue(undefined)
			const { result } = renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports") })
			act(() => result.current.act(row("1"), "delete"))

			await act(async () => result.current.confirmDelete("1"))

			expect(api.deleteReport).toHaveBeenCalledWith("1")
			expect(rows()).toEqual([row("2")])
			expect(result.current.confirmingDelete).toBeNull()
			expect(result.current.busyId).toBeNull()
		})

		it("offers a reload when another reviewer changed the report first", async () => {
			api.publishReport.mockRejectedValue(failure(STALE_REPORT, "stale"))
			const { result } = renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports") })

			await act(async () => result.current.act(row("1"), "publish"))
			expect(result.current.stale).toBe(true)
			expect(result.current.error).toBeNull()

			act(() => result.current.reload())
			expect(result.current.stale).toBe(false)
			expect(list.reload).toHaveBeenCalled()
		})

		it("shows the API's reason for a refused command, else a generic one", async () => {
			api.publishReport.mockRejectedValueOnce(failure("other", "detail")).mockRejectedValueOnce(new Error("no"))
			const { result } = renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports") })

			await act(async () => result.current.act(row("1"), "publish"))
			expect(result.current.error).toBe("detail")
			await act(async () => result.current.act(row("1"), "publish"))
			expect(result.current.error).toBe("reports.error.unexpected")
		})

		it("clears an earlier error when the next command starts, and when reloading", async () => {
			api.publishReport.mockRejectedValueOnce(failure("other", "detail")).mockResolvedValueOnce({ id: "1", status: "published", consent: true, isStuck: false, version: "v9" })
			const { result } = renderHook(() => useManageReportsPage(), { wrapper: at("/admin/reports") })
			await act(async () => result.current.act(row("1"), "publish"))
			expect(result.current.error).toBe("detail")

			await act(async () => result.current.act(row("1"), "publish"))
			expect(result.current.error).toBeNull()

			api.deleteReport.mockRejectedValue(failure("other", "gone"))
			await act(async () => result.current.confirmDelete("1"))
			expect(result.current.error).toBe("gone")
			act(() => result.current.reload())
			expect(result.current.error).toBeNull()
		})
	})
})

describe("ManageReportsPage", () => {
	it("renders its view with the view model", () => {
		render(<ManageReportsPage />, { wrapper: at("/admin/reports?filter=published") })

		expect(screen.getByTestId("view").textContent).toBe("published")
	})
})
