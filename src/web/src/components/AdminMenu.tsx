import { useEffect, useRef, useState } from "react"
import { useAuth } from "../auth/useAuth"
import type { PendingCounts } from "../api/adminReports"
import { AdminMenuView, type AdminMenuViewProps } from "./AdminMenu.view"

export interface AdminMenuProps {
	stacked?: boolean
	onNavigate?: () => void
	counts?: PendingCounts | null
}

export function useAdminMenu({ onNavigate, counts = null }: AdminMenuProps): Omit<AdminMenuViewProps, "stacked"> {
	const { role } = useAuth()
	const [open, setOpen] = useState(false)
	const containerRef = useRef<HTMLDivElement>(null)
	const buttonRef = useRef<HTMLButtonElement>(null)

	useEffect(() => {
		if (!open) return

		function onKeyDown(event: KeyboardEvent) {
			if (event.key === "Escape") {
				setOpen(false)
				buttonRef.current?.focus()
			}
		}

		function onPointerDown(event: PointerEvent) {
			if (!containerRef.current?.contains(event.target as Node)) {
				setOpen(false)
			}
		}

		document.addEventListener("keydown", onKeyDown)
		document.addEventListener("pointerdown", onPointerDown)
		return () => {
			document.removeEventListener("keydown", onKeyDown)
			document.removeEventListener("pointerdown", onPointerDown)
		}
	}, [open])

	function onSelectItem() {
		setOpen(false)
		onNavigate?.()
	}

	return {
		open,
		reports: counts?.reportsNeedingAction ?? 0,
		typeAheadValues: counts?.typeAheadValuesAwaitingReview ?? 0,
		isAdministrator: role === "administrator",
		containerRef,
		buttonRef,
		onToggle: () => setOpen((value) => !value),
		onSelectItem,
	}
}

/**
 * The admin options this member's role allows.
 *
 * Gating the chrome is a convenience, never the boundary — the API authorizes
 * every request on its own, and the admin routes stay reachable by URL on
 * purpose (ADR-0048). Hiding an option a member cannot use just keeps the menu
 * honest about what it offers.
 */
export function AdminMenu(props: AdminMenuProps) {
	return <AdminMenuView stacked={props.stacked} {...useAdminMenu(props)} />
}
