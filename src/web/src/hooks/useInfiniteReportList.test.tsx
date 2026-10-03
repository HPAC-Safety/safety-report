import { act, cleanup, renderHook, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { MemoryRouter } from "react-router-dom"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { useInfiniteReportList, type ReportPage } from "./useInfiniteReportList"

interface Item {
	id: string
}

const item = (id: string): Item => ({ id })
const getId = (value: Item) => value.id

function wrapper({ children }: { children: ReactNode }) {
	return <MemoryRouter>{children}</MemoryRouter>
}

type Fetch = (after: string | null) => Promise<ReportPage<Item>>

interface ObserverStub {
	callback: (entries: { isIntersecting: boolean }[]) => void
	observe: ReturnType<typeof vi.fn>
	disconnect: ReturnType<typeof vi.fn>
}

let observers: ObserverStub[] = []

function setup(fetchPage: Fetch, storageKey = "k") {
	return renderHook(
		(props: { storageKey: string }) => useInfiniteReportList<Item>({ fetchPage, getId, storageKey: props.storageKey }),
		{ wrapper, initialProps: { storageKey } },
	)
}

beforeEach(() => {
	sessionStorage.clear()
	observers = []
	// eslint-disable-next-line @typescript-eslint/no-extraneous-class -- a constructor-only stand-in for the browser's IntersectionObserver
	class FakeObserver {
		constructor(callback: ObserverStub["callback"]) {
			const stub: ObserverStub = { callback, observe: vi.fn(), disconnect: vi.fn() }
			observers.push(stub)
			Object.assign(this, stub)
		}
	}
	vi.stubGlobal("IntersectionObserver", FakeObserver)
	vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
		callback(0)
		return 0
	})
	vi.spyOn(window, "scrollTo").mockImplementation(() => {})
})

afterEach(() => {
	cleanup()
	vi.restoreAllMocks()
	vi.unstubAllGlobals()
})

describe("useInfiniteReportList loading", () => {
	it("loads the first page on mount", async () => {
		const fetchPage = vi.fn<Fetch>().mockResolvedValue({ items: [item("a")], next: "c1" })
		const { result } = setup(fetchPage)
		expect(result.current.initialLoading).toBe(true)
		await waitFor(() => expect(result.current.initialLoading).toBe(false))
		expect(fetchPage).toHaveBeenCalledWith(null)
		expect(result.current.items).toEqual([item("a")])
		expect(result.current.hasMore).toBe(true)
		expect(result.current.announcement).toBe("")
	})

	it("appends the next page, de-duplicates, and announces plural", async () => {
		const fetchPage = vi
			.fn<Fetch>()
			.mockResolvedValueOnce({ items: [item("a")], next: "c1" })
			.mockResolvedValueOnce({ items: [item("a"), item("b"), item("c")], next: null })
		const { result } = setup(fetchPage)
		await waitFor(() => expect(result.current.initialLoading).toBe(false))
		act(() => result.current.loadMore())
		await waitFor(() => expect(result.current.items).toHaveLength(3))
		expect(fetchPage).toHaveBeenLastCalledWith("c1")
		expect(result.current.announcement).toBe("2 more reports loaded.")
		expect(result.current.hasMore).toBe(false)
	})

	it("announces a single new report in the singular", async () => {
		const fetchPage = vi
			.fn<Fetch>()
			.mockResolvedValueOnce({ items: [item("a")], next: "c1" })
			.mockResolvedValueOnce({ items: [item("b")], next: "c2" })
		const { result } = setup(fetchPage)
		await waitFor(() => expect(result.current.initialLoading).toBe(false))
		act(() => result.current.loadMore())
		await waitFor(() => expect(result.current.announcement).toBe("1 more report loaded."))
	})

	it("does not announce when a page adds nothing new", async () => {
		const fetchPage = vi
			.fn<Fetch>()
			.mockResolvedValueOnce({ items: [item("a")], next: "c1" })
			.mockResolvedValueOnce({ items: [item("a")], next: "c2" })
		const { result } = setup(fetchPage)
		await waitFor(() => expect(result.current.initialLoading).toBe(false))
		act(() => result.current.loadMore())
		await waitFor(() => expect(fetchPage).toHaveBeenCalledTimes(2))
		await waitFor(() => expect(result.current.loadingMore).toBe(false))
		expect(result.current.announcement).toBe("")
		expect(result.current.items).toEqual([item("a")])
	})

	it("reports a failure and retries the same cursor", async () => {
		const fetchPage = vi
			.fn<Fetch>()
			.mockResolvedValueOnce({ items: [item("a")], next: "c1" })
			.mockRejectedValueOnce(new Error("nope"))
			.mockResolvedValueOnce({ items: [item("b")], next: null })
		const { result } = setup(fetchPage)
		await waitFor(() => expect(result.current.initialLoading).toBe(false))
		act(() => result.current.loadMore())
		await waitFor(() => expect(result.current.failed).toBe(true))
		act(() => result.current.loadMore())
		await waitFor(() => expect(result.current.items).toHaveLength(2))
		expect(result.current.failed).toBe(false)
		expect(fetchPage).toHaveBeenLastCalledWith("c1")
	})

	it("ignores loadMore while a page is in flight", async () => {
		let resolve: (page: ReportPage<Item>) => void = () => {}
		const fetchPage = vi
			.fn<Fetch>()
			.mockResolvedValueOnce({ items: [item("a")], next: "c1" })
			.mockImplementationOnce(() => new Promise((r) => (resolve = r)))
		const { result } = setup(fetchPage)
		await waitFor(() => expect(result.current.initialLoading).toBe(false))
		act(() => result.current.loadMore())
		await waitFor(() => expect(result.current.loadingMore).toBe(true))
		act(() => result.current.loadMore())
		expect(fetchPage).toHaveBeenCalledTimes(2)
		await act(async () => resolve({ items: [], next: null }))
	})

	it("ignores loadMore during the initial load", () => {
		const fetchPage = vi.fn<Fetch>().mockReturnValue(new Promise(() => {}))
		const { result } = setup(fetchPage)
		act(() => result.current.loadMore())
		expect(fetchPage).toHaveBeenCalledTimes(1)
	})

	it("ignores loadMore when there is no further page", async () => {
		const fetchPage = vi.fn<Fetch>().mockResolvedValue({ items: [item("a")], next: null })
		const { result } = setup(fetchPage)
		await waitFor(() => expect(result.current.initialLoading).toBe(false))
		act(() => result.current.loadMore())
		expect(fetchPage).toHaveBeenCalledTimes(1)
	})
})

describe("useInfiniteReportList mutate and reload", () => {
	it("updates items in place and persists them", async () => {
		const fetchPage = vi.fn<Fetch>().mockResolvedValue({ items: [item("a"), item("b")], next: null })
		const { result } = setup(fetchPage)
		await waitFor(() => expect(result.current.initialLoading).toBe(false))
		act(() => result.current.mutate((items) => items.filter((entry) => entry.id !== "a")))
		expect(result.current.items).toEqual([item("b")])
		const stored = JSON.parse(sessionStorage.getItem("hpac.reportList.k") ?? "{}") as { items: Item[] }
		expect(stored.items).toEqual([item("b")])
	})

	it("reloads from the top", async () => {
		const fetchPage = vi
			.fn<Fetch>()
			.mockResolvedValueOnce({ items: [item("a")], next: null })
			.mockResolvedValueOnce({ items: [item("z")], next: null })
		const { result } = setup(fetchPage)
		await waitFor(() => expect(result.current.initialLoading).toBe(false))
		act(() => result.current.reload())
		await waitFor(() => expect(result.current.items).toEqual([item("z")]))
		expect(fetchPage).toHaveBeenLastCalledWith(null)
	})
})

describe("useInfiniteReportList restoring", () => {
	async function firstVisit(key = "k", scrollY = 120) {
		Object.defineProperty(window, "scrollY", { value: scrollY, configurable: true })
		const fetchPage = vi.fn<Fetch>().mockResolvedValue({ items: [item("a")], next: "c1" })
		const first = setup(fetchPage, key)
		await waitFor(() => expect(first.result.current.initialLoading).toBe(false))
		first.unmount()
	}

	it("restores items and scroll on a return within the same page load", async () => {
		await firstVisit()
		const fetchPage = vi.fn<Fetch>()
		const { result } = setup(fetchPage)
		expect(result.current.initialLoading).toBe(false)
		expect(result.current.items).toEqual([item("a")])
		expect(result.current.hasMore).toBe(true)
		expect(fetchPage).not.toHaveBeenCalled()
		expect(window.scrollTo).toHaveBeenCalledWith(0, 120)
	})

	it("restores a finished list with no more pages", async () => {
		await firstVisit()
		const stored = JSON.parse(sessionStorage.getItem("hpac.reportList.k") ?? "{}") as Record<string, unknown>
		sessionStorage.setItem("hpac.reportList.k", JSON.stringify({ ...stored, next: null }))
		const { result } = setup(vi.fn<Fetch>())
		expect(result.current.hasMore).toBe(false)
	})

	it("starts afresh when saved by an earlier page load of a fresh visit", async () => {
		sessionStorage.setItem(
			"hpac.reportList.k",
			JSON.stringify({ items: [item("old")], next: null, scrollY: 5, entry: "default", document: "other" }),
		)
		const fetchPage = vi.fn<Fetch>().mockResolvedValue({ items: [item("new")], next: null })
		const { result } = setup(fetchPage)
		await waitFor(() => expect(result.current.items).toEqual([item("new")]))
		expect(window.scrollTo).not.toHaveBeenCalled()
	})

	it("restores an earlier page load's list on a reload or back-forward", async () => {
		vi.spyOn(performance, "getEntriesByType").mockReturnValue([{ type: "reload" } as PerformanceNavigationTiming])
		sessionStorage.setItem(
			"hpac.reportList.k",
			JSON.stringify({ items: [item("old")], next: null, scrollY: 5, entry: "default", document: "other" }),
		)
		const { result } = setup(vi.fn<Fetch>())
		expect(result.current.items).toEqual([item("old")])
	})

	it("treats a missing navigation entry as a fresh visit", () => {
		vi.spyOn(performance, "getEntriesByType").mockReturnValue([])
		sessionStorage.setItem(
			"hpac.reportList.k",
			JSON.stringify({ items: [item("old")], next: null, scrollY: 5, entry: "default", document: "other" }),
		)
		const { result } = setup(vi.fn<Fetch>().mockReturnValue(new Promise(() => {})))
		expect(result.current.items).toEqual([])
	})

	it("treats a failing navigation lookup as a fresh visit", () => {
		vi.spyOn(performance, "getEntriesByType").mockImplementation(() => {
			throw new Error("unsupported")
		})
		sessionStorage.setItem(
			"hpac.reportList.k",
			JSON.stringify({ items: [item("old")], next: null, scrollY: 5, entry: "default", document: "other" }),
		)
		const { result } = setup(vi.fn<Fetch>().mockReturnValue(new Promise(() => {})))
		expect(result.current.items).toEqual([])
	})

	it("does not restore a list saved in a different history entry", () => {
		sessionStorage.setItem(
			"hpac.reportList.k",
			JSON.stringify({ items: [item("old")], next: null, scrollY: 5, entry: "elsewhere", document: "other" }),
		)
		const { result } = setup(vi.fn<Fetch>().mockReturnValue(new Promise(() => {})))
		expect(result.current.items).toEqual([])
	})

	it("ignores unparseable stored state", async () => {
		sessionStorage.setItem("hpac.reportList.k", "{not json")
		const fetchPage = vi.fn<Fetch>().mockResolvedValue({ items: [item("a")], next: null })
		const { result } = setup(fetchPage)
		await waitFor(() => expect(result.current.items).toEqual([item("a")]))
	})

	it("survives storage refusing a write", async () => {
		vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
			throw new Error("quota")
		})
		const fetchPage = vi.fn<Fetch>().mockResolvedValue({ items: [item("a")], next: null })
		const { result } = setup(fetchPage)
		await waitFor(() => expect(result.current.items).toEqual([item("a")]))
	})

	it("starts a fresh list when the storage key changes", async () => {
		await firstVisit("one")
		const fetchPage = vi
			.fn<Fetch>()
			.mockResolvedValue({ items: [item("b")], next: null })
		const { result, rerender } = setup(fetchPage, "two")
		await waitFor(() => expect(result.current.items).toEqual([item("b")]))
		rerender({ storageKey: "one" })
		await waitFor(() => expect(result.current.items).toEqual([item("a")]))
		expect(result.current.hasMore).toBe(true)
		rerender({ storageKey: "three" })
		await waitFor(() => expect(result.current.items).toEqual([item("b")]))
	})

	it("resets state for a key with nothing saved", async () => {
		const fetchPage = vi.fn<Fetch>().mockResolvedValue({ items: [item("a")], next: null })
		const { result, rerender } = setup(fetchPage, "one")
		await waitFor(() => expect(result.current.items).toEqual([item("a")]))
		fetchPage.mockReturnValue(new Promise(() => {}))
		rerender({ storageKey: "two" })
		await waitFor(() => expect(result.current.items).toEqual([]))
		expect(result.current.initialLoading).toBe(true)
	})
})

describe("useInfiniteReportList sentinel", () => {
	it("loads more when the sentinel intersects", async () => {
		const fetchPage = vi
			.fn<Fetch>()
			.mockResolvedValueOnce({ items: [item("a")], next: "c1" })
			.mockResolvedValueOnce({ items: [item("b")], next: null })
		const { result } = setup(fetchPage)
		await waitFor(() => expect(result.current.initialLoading).toBe(false))
		const node = document.createElement("div")
		act(() => result.current.sentinelRef(node))
		expect(observers[0].observe).toHaveBeenCalledWith(node)
		act(() => observers[0].callback([{ isIntersecting: false }]))
		expect(fetchPage).toHaveBeenCalledTimes(1)
		act(() => observers[0].callback([{ isIntersecting: true }]))
		await waitFor(() => expect(result.current.items).toHaveLength(2))
	})

	it("disconnects the previous observer when the node changes or clears", async () => {
		const fetchPage = vi.fn<Fetch>().mockResolvedValue({ items: [item("a")], next: null })
		const { result } = setup(fetchPage)
		await waitFor(() => expect(result.current.initialLoading).toBe(false))
		act(() => result.current.sentinelRef(document.createElement("div")))
		act(() => result.current.sentinelRef(document.createElement("div")))
		expect(observers).toHaveLength(2)
		expect(observers[0].disconnect).toHaveBeenCalled()
		act(() => result.current.sentinelRef(null))
		expect(observers[1].disconnect).toHaveBeenCalled()
		expect(observers).toHaveLength(2)
	})

	it("disconnects the observer on unmount", async () => {
		const fetchPage = vi.fn<Fetch>().mockResolvedValue({ items: [item("a")], next: null })
		const { result, unmount } = setup(fetchPage)
		await waitFor(() => expect(result.current.initialLoading).toBe(false))
		act(() => result.current.sentinelRef(document.createElement("div")))
		unmount()
		expect(observers[0].disconnect).toHaveBeenCalled()
	})
})
