import type { ReactNode } from "react"
import { useLocale } from "../i18n/useLocale"
import { UnsavedChangesDialog } from "../components/UnsavedChangesDialog"
import { UnsavedChangesContext, type Registry, type UnsavedChangesCopy } from "./useUnsavedChangesGuard"

export interface UnsavedChangesGuardRootViewProps {
	registry: Registry
	/** A navigation is blocked and waiting on the person's answer. */
	blocked: boolean
	/** The wording of the form that blocked the navigation, if it brought its own. */
	copy: UnsavedChangesCopy | undefined
	onConfirm: () => void
	onKeep: () => void
	children: ReactNode
}

export function UnsavedChangesGuardRootView({ registry, blocked, copy, onConfirm, onKeep, children }: UnsavedChangesGuardRootViewProps) {
	const { t } = useLocale()

	return (
		<UnsavedChangesContext.Provider value={registry}>
			{children}
			{blocked && <UnsavedChangesDialog onConfirm={onConfirm} onKeep={onKeep} t={t} copy={copy} />}
		</UnsavedChangesContext.Provider>
	)
}
