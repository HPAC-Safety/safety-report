import { useEffect, useRef } from "react"

/**
 * Warns before leaving the page while `active` is true: the browser's own
 * prompt on a close or reload, and a confirmation before any in-app
 * navigation — a link click, or the back/forward buttons. Generic and small
 * on purpose (issue 658): the app has no data router here to hang a
 * navigation blocker off, so this hook intercepts clicks and `popstate`
 * itself. Issue 659 will decide whether every form gets one of these; for
 * now only the private-attachment staging area does.
 */
export function useLeaveWarning(active: boolean, message: string) {
	const activeRef = useRef(active)
	activeRef.current = active

	useEffect(() => {
		function onBeforeUnload(event: BeforeUnloadEvent) {
			if (!activeRef.current) return
			event.preventDefault()
			// Chrome still requires this for the native prompt to appear; the
			// text itself is never shown, every browser supplies its own.
			event.returnValue = ""
		}

		function sameDocument(url: URL): boolean {
			return url.origin === window.location.origin && url.pathname === window.location.pathname && url.search === window.location.search
		}

		function onClick(event: MouseEvent) {
			if (!activeRef.current || event.defaultPrevented || event.button !== 0) return
			if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return // Opening in a new tab leaves this one alone.
			const anchor = (event.target as HTMLElement | null)?.closest?.("a[href]") as HTMLAnchorElement | null
			if (!anchor || anchor.target === "_blank") return

			let url: URL
			try {
				url = new URL(anchor.href, window.location.href)
			} catch {
				return
			}
			if (url.origin !== window.location.origin || sameDocument(url)) return

			if (!window.confirm(message)) {
				event.preventDefault()
				event.stopImmediatePropagation()
			}
		}

		// Undoes a back/forward navigation the person declines: popstate fires
		// only after the URL has already changed, so the way to "block" it is to
		// immediately return to where it came from.
		let restoring = false
		function onPopState() {
			if (!activeRef.current || restoring) return
			if (window.confirm(message)) return
			restoring = true
			history.forward()
			window.setTimeout(() => {
				restoring = false
			}, 0)
		}

		window.addEventListener("beforeunload", onBeforeUnload)
		document.addEventListener("click", onClick, true)
		window.addEventListener("popstate", onPopState)
		return () => {
			window.removeEventListener("beforeunload", onBeforeUnload)
			document.removeEventListener("click", onClick, true)
			window.removeEventListener("popstate", onPopState)
		}
	}, [message])
}
