import { useEffect, useRef, useState } from "react"
import logoLight from "../../assets/hpac-light.svg"
import logoDark from "../../assets/hpac-dark.svg"
import { useAuth } from "../auth/useAuth"
import { useTheme } from "../theme/useTheme"
import { usePendingCounts } from "./usePendingCounts"
import { HeaderView, type HeaderViewProps } from "./Header.view"

function prefersDark(): boolean {
	return typeof window !== "undefined" && window.matchMedia("(prefers-color-scheme: dark)").matches
}

export function useHeader(): HeaderViewProps {
	const { isSignedIn, role, signOut } = useAuth()
	const { theme } = useTheme()
	const [menuOpen, setMenuOpen] = useState(false)
	const toggleButtonRef = useRef<HTMLButtonElement>(null)
	const effectiveDark = theme === "dark" || (theme === null && prefersDark())
	const logo = effectiveDark ? logoDark : logoLight
	const isReviewer = isSignedIn && role !== "user"
	// Read once here, not in each AdminMenu: the header draws the menu twice.
	const pendingCounts = usePendingCounts(isReviewer)

	useEffect(() => {
		if (!menuOpen) return

		function onKeyDown(event: KeyboardEvent) {
			if (event.key === "Escape") {
				setMenuOpen(false)
				toggleButtonRef.current?.focus()
			}
		}

		document.addEventListener("keydown", onKeyDown)
		return () => document.removeEventListener("keydown", onKeyDown)
	}, [menuOpen])

	return {
		logo,
		isSignedIn,
		isReviewer,
		pendingCounts,
		menuOpen,
		toggleButtonRef,
		onSignOut: signOut,
		onSignOutAndClose: () => {
			signOut()
			setMenuOpen(false)
		},
		onToggleMenu: () => setMenuOpen((open) => !open),
		onCloseMenu: () => setMenuOpen(false),
	}
}

export function Header() {
	return <HeaderView {...useHeader()} />
}
