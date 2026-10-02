import { useMemo, useRef, type ReactNode } from "react"
import { useBlocker } from "react-router-dom"
import { UnsavedChangesGuardRootView, type UnsavedChangesGuardRootViewProps } from "./UnsavedChangesGuardRoot.view"
import type { Registration, Registry, UnsavedChangesCopy } from "./useUnsavedChangesGuard"

export function useUnsavedChangesGuardRoot(): Omit<UnsavedChangesGuardRootViewProps, "children"> {
	const registrations = useRef(new Map<string, Registration>())
	// The wording of the form that blocked the navigation being confirmed.
	const blockedCopy = useRef<UnsavedChangesCopy | undefined>(undefined)

	const registry = useMemo<Registry>(
		() => ({
			register: (id, registration) => registrations.current.set(id, registration),
			unregister: (id) => registrations.current.delete(id),
		}),
		[],
	)

	const blocker = useBlocker((args) => {
		for (const registration of registrations.current.values()) {
			if (registration.shouldBlock(args)) {
				blockedCopy.current = registration.copy?.()
				return true
			}
		}
		return false
	})

	return {
		registry,
		blocked: blocker.state === "blocked",
		copy: blockedCopy.current,
		onConfirm: () => blocker.proceed?.(),
		onKeep: () => blocker.reset?.(),
	}
}

export function UnsavedChangesGuardRoot({ children }: { children: ReactNode }) {
	return <UnsavedChangesGuardRootView {...useUnsavedChangesGuardRoot()}>{children}</UnsavedChangesGuardRootView>
}
