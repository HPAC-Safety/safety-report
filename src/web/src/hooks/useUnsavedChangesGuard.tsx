import { createContext, useContext, useEffect, useId, useMemo, useRef, type ReactNode } from "react"
import { useBlocker } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { UnsavedChangesDialog } from "../components/UnsavedChangesDialog"

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

interface Registry {
	register: (id: string, predicate: ShouldBlock) => void
	unregister: (id: string) => void
}

const UnsavedChangesContext = createContext<Registry | null>(null)

function isWithinPath(pathname: string, withinPath: string): boolean {
	return pathname === withinPath || pathname.startsWith(`${withinPath}/`)
}

export function UnsavedChangesGuardRoot({ children }: { children: ReactNode }) {
	const { t } = useLocale()
	const predicates = useRef(new Map<string, ShouldBlock>())

	const registry = useMemo<Registry>(
		() => ({
			register: (id, predicate) => predicates.current.set(id, predicate),
			unregister: (id) => predicates.current.delete(id),
		}),
		[],
	)

	const blocker = useBlocker((args) => {
		for (const predicate of predicates.current.values()) {
			if (predicate(args)) return true
		}
		return false
	})

	return (
		<UnsavedChangesContext.Provider value={registry}>
			{children}
			{blocker.state === "blocked" && (
				<UnsavedChangesDialog onConfirm={() => blocker.proceed()} onKeep={() => blocker.reset()} t={t} />
			)}
		</UnsavedChangesContext.Provider>
	)
}

/**
 * `withinPath`, when given, lets navigation *within* that path proceed
 * without blocking. A multi-step form has one route per step (ADR-0099), so
 * moving between its own steps changes the address without leaving the form;
 * only a route outside `withinPath` counts as leaving it.
 */
export function useUnsavedChangesGuard(dirty: boolean, withinPath?: string): void {
	const registry = useContext(UnsavedChangesContext)
	const id = useId()

	useEffect(() => {
		if (!dirty) return
		const handler = (event: BeforeUnloadEvent) => {
			event.preventDefault()
			// Chrome and most browsers ignore this value and show their own
			// generic prompt (no custom text is possible); the assignment is
			// only here for older browsers that still read it.
			event.returnValue = ""
		}
		window.addEventListener("beforeunload", handler)
		return () => window.removeEventListener("beforeunload", handler)
	}, [dirty])

	useEffect(() => {
		if (!registry || !dirty) return
		registry.register(id, ({ currentLocation, nextLocation }) => {
			if (nextLocation.pathname === currentLocation.pathname) return false
			if (withinPath && isWithinPath(nextLocation.pathname, withinPath) && isWithinPath(currentLocation.pathname, withinPath)) {
				return false
			}
			return true
		})
		return () => registry.unregister(id)
	}, [registry, id, dirty, withinPath])
}
