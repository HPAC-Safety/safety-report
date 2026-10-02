import { useLayoutEffect } from "react"
import { useLocation, useNavigationType } from "react-router-dom"
import { ScrollToTopOnNavigationView } from "./ScrollToTopOnNavigation.view"

/*
 * Starts every page at its top when the reader navigates to it afresh
 * (ADR-0173, REQ-WLD-032). `<BrowserRouter>` resets nothing by itself,
 * so without this a new page opens at the old page's scroll position:
 * following a footer link from the bottom of one page lands at the bottom of
 * the next.
 *
 * It resets only when the path changes on a push or a replace. A change to the
 * query string alone (the search box, a status filter) keeps the reader where
 * they are. A link to an in-page anchor is left to the browser. Back and
 * Forward (a "POP", which is also how a page first loads) are left to the
 * browser and to a report list's own restoration (REQ-MOD-082).
 *
 * Rendered once, beside the routes, so it applies to every page and no page
 * opts in or out.
 */
export function useScrollToTopOnNavigation(): Record<string, never> {
	const { pathname, hash } = useLocation()
	const navigationType = useNavigationType()

	// Before paint, so the new page never flashes at the old position.
	useLayoutEffect(() => {
		if (navigationType === "POP" || hash) return
		window.scrollTo({ top: 0, left: 0, behavior: "instant" })
		// A new page resets; the navigation type and anchor are read for that change only.
		// eslint-disable-next-line react-hooks/exhaustive-deps -- see the comment above: only a pathname change resets
	}, [pathname])

	return {}
}

export function ScrollToTopOnNavigation() {
	return <ScrollToTopOnNavigationView {...useScrollToTopOnNavigation()} />
}
