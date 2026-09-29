import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { useLocation } from "react-router-dom"

/*
 * Infinite scroll for a keyset-paginated report list — the public feed and
 * Manage reports both use it (issue no. 572). It:
 *
 * - loads the next page automatically as an IntersectionObserver sentinel
 *   nears the viewport, with a visible "Load more" button underneath that
 *   works the same way for a keyboard or screen-reader visitor, and reads
 *   "Retry" once a page fails to load;
 * - de-duplicates appended items by ID, because a cursor naming a report that
 *   is no longer in the list (unpublished, deleted, or filtered out since) is
 *   answered from the top instead — this keeps that restart from ever
 *   showing an item twice;
 * - politely announces how many new items just loaded, for a screen-reader
 *   visitor who is not watching the page;
 * - restores the same accumulated results and scroll position on a back-
 *   button return, keyed by `storageKey` (which should fold in every query
 *   parameter the caller's page reads, such as a search term or a status
 *   filter) — it does not refetch in that case, since what was there is still
 *   valid until the caller changes the key. It restores only on a return to
 *   the history entry the list was built in; a fresh visit, from a link or by
 *   typing the address again, loads the first page and starts at the top
 *   (issue no. 670).
 */

export interface ReportPage<T> {
	items: T[]
	next: string | null
}

export interface UseInfiniteReportListOptions<T> {
	/**
	 * Fetches one page. `null` asks for the first page.
	 *
	 * A method signature rather than an arrow-typed property, on purpose:
	 * tools/check-hardcoded-strings.mjs is a line scanner that misreads
	 * `=> Promise<...>` on one line — see adminQuestions.ts.
	 */
	fetchPage(after: string | null): Promise<ReportPage<T>>
	/** The stable identity of an item, for de-duplication across pages. */
	getId: (item: T) => string
	/**
	 * Distinguishes one accumulated list from another in `sessionStorage` — fold
	 * in every query parameter the page itself reads (a filter, `q`, …), so a
	 * changed filter starts a fresh list rather than restoring a stale one.
	 */
	storageKey: string
}

export interface UseInfiniteReportListResult<T> {
	items: T[]
	/** The very first page has not resolved yet. */
	initialLoading: boolean
	/** A later page is in flight. */
	loadingMore: boolean
	/** The most recent page request failed; retry with `loadMore`. */
	failed: boolean
	/** Whether another page might exist. */
	hasMore: boolean
	/** Loads the next page, or retries the last one after a failure. */
	loadMore: () => void
	/** A ref callback for the sentinel element placed after the last item. */
	sentinelRef: (node: Element | null) => void
	/** Text for a polite `aria-live` region, cleared between announcements. */
	announcement: string
	/**
	 * Updates the accumulated items in place — a quick action changing or
	 * removing a row, never a fetch — and re-persists them under the same key so
	 * a later back-button return still restores the edited list.
	 */
	mutate: (updater: (items: T[]) => T[]) => void
	/** Discards the accumulated list and reloads it from the top. */
	reload: () => void
}

interface StoredState<T> {
	items: T[]
	next: string | null
	scrollY: number
	/** The history entry the list was built in: React Router's `location.key`. */
	entry: string
	/** The page load that saved it: {@link DOCUMENT}. */
	document: string
}

/**
 * Tells this page load apart from an earlier one in the same tab. React Router
 * names the first history entry of every page load "default", so the entry
 * alone cannot tell a Back return to that first entry (restore) from typing
 * the list's address again in a new page load (start afresh).
 */
const DOCUMENT = `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`

/** How the browser arrived at this page load: "navigate", "reload", or "back_forward". */
function documentNavigationType(): string {
	try {
		const [entry] = performance.getEntriesByType("navigation") as PerformanceNavigationTiming[]
		return entry?.type ?? "navigate"
	} catch {
		return "navigate"
	}
}

// Signature split across lines on purpose: tools/check-hardcoded-strings.mjs
// is a line scanner — see adminQuestions.ts.
function readStored<T>(
	key: string,
): StoredState<T> | null {
	try {
		const raw = window.sessionStorage.getItem(key)
		return raw ? (JSON.parse(raw) as StoredState<T>) : null
	} catch {
		return null
	}
}

/**
 * The saved list for this key, only on a return to the history entry it was
 * saved in: Back or Forward inside the site, a reload, or Back into the site
 * from elsewhere. A fresh visit gets nothing, so it loads the first page and
 * starts at the top instead of reusing a position saved earlier in the tab.
 */
function readRestorable<T>(
	key: string,
	entry: string,
): StoredState<T> | null {
	const stored = readStored<T>(key)
	if (!stored || stored.entry !== entry) {
		return null
	}
	return stored.document === DOCUMENT || documentNavigationType() !== "navigate" ? stored : null
}

function writeStored<T>(
	key: string,
	state: StoredState<T>,
) {
	try {
		window.sessionStorage.setItem(key, JSON.stringify(state))
	} catch {
		// Private browsing or a full quota: the list still works, it just will
		// not restore its position on a later back-button return.
	}
}

export function useInfiniteReportList<T>(
	options: UseInfiniteReportListOptions<T>,
): UseInfiniteReportListResult<T> {
	const { fetchPage, getId, storageKey } = options
	const key = `hpac.reportList.${storageKey}`
	const { key: entry } = useLocation()
	const entryRef = useRef(entry)
	entryRef.current = entry
	const restored = useMemo(() => {
		return readRestorable<T>(key, entry)
		// Decided once per list key: a later entry change on the same list does not re-restore.
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [key])

	const [items, setItems] = useState<T[]>(restored?.items ?? [])
	const [next, setNext] = useState<string | null>(restored?.next ?? null)
	const [initialLoading, setInitialLoading] = useState(restored === null)
	const [loadingMore, setLoadingMore] = useState(false)
	const [failed, setFailed] = useState(false)
	const [announcement, setAnnouncement] = useState("")
	const [hasMore, setHasMore] = useState(restored === null || restored.next !== null)

	const fetchPageRef = useRef(fetchPage)
	fetchPageRef.current = fetchPage
	const getIdRef = useRef(getId)
	getIdRef.current = getId
	const restoredScroll = useRef(restored?.scrollY ?? null)

	// A fresh key (a changed filter or search term) starts over; it never
	// shares another key's accumulated items.
	useEffect(() => {
		const again = readRestorable<T>(key, entryRef.current)
		setItems(again?.items ?? [])
		setNext(again?.next ?? null)
		setHasMore(again === null || again.next !== null)
		setInitialLoading(again === null)
		setFailed(false)
		restoredScroll.current = again?.scrollY ?? null
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [key])

	const persist = useCallback(
		(nextItems: T[], nextCursor: string | null) => {
			writeStored(key, {
				items: nextItems,
				next: nextCursor,
				scrollY: window.scrollY,
				entry: entryRef.current,
				document: DOCUMENT,
			})
		},
		[key],
	)

	const load = useCallback(
		(after: string | null, replace: boolean) => {
			if (replace) {
				setInitialLoading(true)
			} else {
				setLoadingMore(true)
			}
			setFailed(false)

			fetchPageRef
				.current(after)
				.then((page) => {
					setItems((current) => {
						const seen = new Set(replace ? [] : current.map(getIdRef.current))
						const added = page.items.filter((item) => !seen.has(getIdRef.current(item)))
						const merged = replace ? added : [...current, ...added]
						setNext(page.next)
						setHasMore(page.next !== null)
						persist(merged, page.next)
						if (!replace && added.length > 0) {
							setAnnouncement(added.length === 1 ? "1 more report loaded." : `${added.length} more reports loaded.`)
						}
						return merged
					})
				})
				.catch(() => {
					setFailed(true)
				})
				.finally(() => {
					setInitialLoading(false)
					setLoadingMore(false)
				})
		},
		[persist],
	)

	// The very first page, only when nothing was restored for this key.
	useEffect(() => {
		if (restored === null) {
			load(null, true)
		}
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [key])

	// Restores scroll position once the restored items have painted.
	useEffect(() => {
		if (restoredScroll.current !== null) {
			const y = restoredScroll.current
			restoredScroll.current = null
			requestAnimationFrame(() => window.scrollTo(0, y))
		}
	}, [items])

	const loadMore = useCallback(() => {
		if (loadingMore || initialLoading) {
			return
		}
		if (!failed && !hasMore) {
			return
		}
		load(next, false)
	}, [failed, hasMore, initialLoading, load, loadingMore, next])

	const observerRef = useRef<IntersectionObserver | null>(null)

	const sentinelRef = useCallback(
		(node: Element | null) => {
			observerRef.current?.disconnect()
			observerRef.current = null

			if (!node) {
				return
			}

			observerRef.current = new IntersectionObserver((entries) => {
				if (entries.some((entry) => entry.isIntersecting)) {
					loadMore()
				}
			})
			observerRef.current.observe(node)
		},
		[loadMore],
	)

	useEffect(() => {
		return () => observerRef.current?.disconnect()
	}, [])

	const mutate = useCallback(
		(updater: (current: T[]) => T[]) => {
			setItems((current) => {
				const updated = updater(current)
				persist(updated, next)
				return updated
			})
		},
		[next, persist],
	)

	const reload = useCallback(() => {
		load(null, true)
	}, [load])

	return { items, initialLoading, loadingMore, failed, hasMore, loadMore, sentinelRef, announcement, mutate, reload }
}
