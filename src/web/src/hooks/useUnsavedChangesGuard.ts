import { createContext, useContext, useEffect, useId } from "react"

/**
 * Warns before a person loses unsaved changes on a form (issue no. 659): a
 * browser unload (closing the tab, reloading, typing a new address) shows
 * the browser's own prompt, and an in-app route change shows a shared
 * bilingual confirm dialog instead.
 *
 * React Router supports only one active `useBlocker` per router, so every
 * form cannot call it independently. `UnsavedChangesGuardRoot` — mounted
 * once, in `App.tsx` — owns the one blocker and the one dialog; each form's
 * `useUnsavedChangesGuard(dirty, withinPath?)` just registers its own
 * predicate with it and unregisters when it becomes clean or unmounts.
 */

type Location = { pathname: string }
type ShouldBlock = (args: { currentLocation: Location; nextLocation: Location }) => boolean

/** The dialog's own wording, already translated. Absent: the shared `unsavedChanges.*` wording. */
export interface UnsavedChangesCopy {
	title: string
	body: string
	leave: string
	stay: string
}

export interface UnsavedChangesOptions {
	/** Moving within this path never blocks; see `useUnsavedChangesGuard`. */
	withinPath?: string
	/** False skips the browser's own unload prompt, for a form whose changes survive an unload. */
	unloadPrompt?: boolean
	/** Read when a navigation is blocked, so it can name what is saved at that moment; undefined falls back to the shared wording. */
	copy?: () => UnsavedChangesCopy | undefined
}

export interface Registration {
	shouldBlock: ShouldBlock
	copy?: () => UnsavedChangesCopy | undefined
}

export interface Registry {
	register: (id: string, registration: Registration) => void
	unregister: (id: string) => void
}

export const UnsavedChangesContext = createContext<Registry | null>(null)

function isWithinPath(pathname: string, withinPath: string): boolean {
	return pathname === withinPath || pathname.startsWith(`${withinPath}/`)
}

/**
 * `withinPath`, when given, lets navigation *within* that path proceed
 * without blocking. A multi-step form has one route per step (ADR-0099), so
 * moving between its own steps changes the address without leaving the form;
 * only a route outside `withinPath` counts as leaving it.
 *
 * `unloadPrompt: false` and `copy` are for a form whose changes are kept
 * anyway: the report form, saved in the browser for 15 days (issue no. 748),
 * says so instead of warning of a loss that does not happen.
 */
export function useUnsavedChangesGuard(dirty: boolean, options: UnsavedChangesOptions = {}): void {
	const { withinPath, unloadPrompt = true, copy } = options
	const registry = useContext(UnsavedChangesContext)
	const id = useId()

	useEffect(() => {
		if (!dirty || !unloadPrompt) return
		const handler = (event: BeforeUnloadEvent) => {
			event.preventDefault()
			// Chrome and most browsers ignore this value and show their own
			// generic prompt (no custom text is possible); the assignment is
			// only here for older browsers that still read it.
			// eslint-disable-next-line @typescript-eslint/no-deprecated -- the legacy assignment is the only prompt trigger older browsers read
			event.returnValue = ""
		}
		window.addEventListener("beforeunload", handler)
		return () => window.removeEventListener("beforeunload", handler)
	}, [dirty, unloadPrompt])

	useEffect(() => {
		if (!registry || !dirty) return
		registry.register(id, {
			shouldBlock: ({ currentLocation, nextLocation }) => {
				if (nextLocation.pathname === currentLocation.pathname) return false
				if (withinPath && isWithinPath(nextLocation.pathname, withinPath) && isWithinPath(currentLocation.pathname, withinPath)) {
					return false
				}
				return true
			},
			copy,
		})
		return () => registry.unregister(id)
	}, [registry, id, dirty, withinPath, copy])
}
