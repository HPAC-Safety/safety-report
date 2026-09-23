import { useEffect, useRef, useState } from "react"
import { Link } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { useAuth } from "../auth/useAuth"

const rowLinkClassName =
	"touch-target flex items-center whitespace-nowrap rounded px-4 font-sans text-sm font-medium text-ink underline-offset-4 hover:underline"

const stackedLinkClassName =
	"touch-target flex items-center rounded px-2 font-sans text-base font-medium text-ink underline-offset-4 hover:underline"

/**
 * The admin options this member's role allows.
 *
 * Gating the chrome is a convenience, never the boundary — the API authorizes
 * every request on its own, and the admin routes stay reachable by URL on
 * purpose (ADR-0048). Hiding an option a member cannot use just keeps the menu
 * honest about what it offers.
 */
export function AdminMenu({ stacked = false, onNavigate }: { stacked?: boolean; onNavigate?: () => void }) {
	const { t } = useLocale()
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

	function selectItem() {
		setOpen(false)
		onNavigate?.()
	}

	return (
		<div ref={containerRef} className="relative">
			<button
				ref={buttonRef}
				type="button"
				onClick={() => setOpen((value) => !value)}
				aria-haspopup="menu"
				aria-expanded={open}
				className={stacked ? stackedLinkClassName : "touch-target inline-flex items-center rounded px-2 font-sans text-sm font-medium text-ink underline-offset-4 hover:underline"}
			>
				{t("nav.admin")}
			</button>

			{open && (
				<div
					role="menu"
					aria-label={t("nav.admin")}
					className={
						stacked
							? "mt-1 flex flex-col gap-1 border-l border-rule pl-4"
							: "absolute right-0 top-full z-50 mt-1 flex w-max flex-col gap-1 rounded border border-rule bg-surface py-2 shadow-lg"
					}
				>
					<Link role="menuitem" to="/admin/reports" onClick={selectItem} className={stacked ? stackedLinkClassName : rowLinkClassName}>
						{t("nav.manageReports")}
					</Link>
					{role === "administrator" && (
						<>
							<Link role="menuitem" to="/admin/questions" onClick={selectItem} className={stacked ? stackedLinkClassName : rowLinkClassName}>
								{t("nav.manageQuestions")}
							</Link>
							<Link role="menuitem" to="/admin/answer-translations" onClick={selectItem} className={stacked ? stackedLinkClassName : rowLinkClassName}>
								{t("nav.manageAnswerTranslations")}
							</Link>
						</>
					)}
				</div>
			)}
		</div>
	)
}
