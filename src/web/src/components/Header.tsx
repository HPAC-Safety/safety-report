import { useEffect, useRef, useState } from "react"
import { Link } from "react-router-dom"
import logo from "../../assets/hpac-logo.png"
import { useLocale } from "../i18n/useLocale"
import { useAuth } from "../auth/useAuth"
import { Nav } from "./Nav"
import { LanguageToggle } from "./LanguageToggle"
import { ThemeToggle } from "./ThemeToggle"

function MenuIcon() {
	return (
		<svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
			<path d="M4 6h16M4 12h16M4 18h16" />
		</svg>
	)
}

function CloseIcon() {
	return (
		<svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
			<path d="M6 6l12 12M18 6L6 18" />
		</svg>
	)
}

export function Header() {
	const { t } = useLocale()
	const { isSignedIn, signOut } = useAuth()
	const [menuOpen, setMenuOpen] = useState(false)
	const toggleButtonRef = useRef<HTMLButtonElement>(null)

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

	return (
		<header className="relative border-b border-rule bg-surface">
			<div className="relative z-50 mx-auto flex max-w-7xl items-center justify-between gap-4 px-6 py-4">
				<Link to="/" className="touch-target inline-flex items-center rounded bg-logo-plate px-3 py-2">
					<img src={logo} alt={t("header.logoAlt")} width={260} height={125} className="h-10 w-auto" />
				</Link>

				<div className="hidden flex-wrap items-center justify-end gap-x-4 gap-y-2 lg:flex">
					<Nav />
					<div className="flex items-center gap-1">
						<LanguageToggle />
						<ThemeToggle />
					</div>
					{isSignedIn ? (
						<button
							type="button"
							onClick={signOut}
							className="touch-target inline-flex items-center rounded bg-brand-700 px-4 font-sans text-sm font-semibold text-ink-inverse"
						>
							{t("nav.memberLogout")}
						</button>
					) : (
						<Link
							to="/login"
							className="touch-target inline-flex items-center rounded bg-brand-700 px-4 font-sans text-sm font-semibold text-ink-inverse"
						>
							{t("nav.memberLogin")}
						</Link>
					)}
				</div>

				<button
					ref={toggleButtonRef}
					type="button"
					onClick={() => setMenuOpen((open) => !open)}
					aria-haspopup="dialog"
					aria-expanded={menuOpen}
					className="touch-target inline-flex items-center justify-center rounded text-ink lg:hidden"
				>
					{menuOpen ? <CloseIcon /> : <MenuIcon />}
					<span className="sr-only">{menuOpen ? t("nav.closeMenuLabel") : t("nav.openMenuLabel")}</span>
				</button>
			</div>

			{menuOpen && (
				<>
					<div onClick={() => setMenuOpen(false)} aria-hidden="true" className="fixed inset-0 z-40 bg-black/40 lg:hidden" />

					<div
						role="dialog"
						aria-modal="true"
						aria-label={t("nav.primaryLabel")}
						className="absolute inset-x-0 top-full z-50 flex flex-col border-b border-rule bg-surface lg:hidden"
					>
						<div className="flex items-center justify-end gap-1 border-b border-rule px-6 py-3">
							<LanguageToggle />
							<ThemeToggle />
						</div>

						<div className="flex flex-col gap-1 px-6 py-4">
							<Nav stacked onNavigate={() => setMenuOpen(false)} />
						</div>

						{isSignedIn ? (
							<button
								type="button"
								onClick={() => {
									signOut()
									setMenuOpen(false)
								}}
								className="touch-target flex items-center justify-center bg-brand-700 py-4 font-sans text-sm font-semibold text-ink-inverse"
							>
								{t("nav.memberLogout")}
							</button>
						) : (
							<Link
								to="/login"
								onClick={() => setMenuOpen(false)}
								className="touch-target flex items-center justify-center bg-brand-700 py-4 font-sans text-sm font-semibold text-ink-inverse"
							>
								{t("nav.memberLogin")}
							</Link>
						)}
					</div>
				</>
			)}
		</header>
	)
}
