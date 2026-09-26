import { useEffect, useRef, useState } from "react"
import { Link } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { useAuth } from "../auth/useAuth"
import type { PendingCounts } from "../api/adminReports"
import { CountBadge } from "./CountBadge"

const rowLinkClassName =
	"group touch-target flex items-center whitespace-nowrap rounded px-4 font-sans text-sm font-medium text-ink"

const stackedLinkClassName =
	"group touch-target flex items-center rounded px-2 font-sans text-base font-medium text-ink"

/** Underlines only the words on hover, never the count beside them. */
function Label({ children }: { children: string }) {
	return <span className="underline-offset-4 group-hover:underline">{children}</span>
}

/**
 * The admin options this member's role allows.
 *
 * Gating the chrome is a convenience, never the boundary — the API authorizes
 * every request on its own, and the admin routes stay reachable by URL on
 * purpose (ADR-0048). Hiding an option a member cannot use just keeps the menu
 * honest about what it offers.
 */
export function AdminMenu({
	stacked = false,
	onNavigate,
	counts = null,
}: {
	stacked?: boolean
	onNavigate?: () => void
	counts?: PendingCounts | null
}) {
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

	const reports = counts?.reportsNeedingAction ?? 0
	const translations = role === "administrator" ? (counts?.answersAwaitingTranslation ?? 0) : 0
	const typeAheadValues = counts?.typeAheadValuesAwaitingReview ?? 0

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
				className={stacked ? stackedLinkClassName : "group touch-target inline-flex items-center rounded px-2 font-sans text-sm font-medium text-ink"}
			>
				<Label>{t("nav.admin")}</Label>
				<CountBadge count={reports + translations + typeAheadValues} />
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
						<Label>{t("nav.manageReports")}</Label>
						<CountBadge count={reports} />
					</Link>
					<Link role="menuitem" to="/admin/type-ahead-values" onClick={selectItem} className={stacked ? stackedLinkClassName : rowLinkClassName}>
						<Label>{t("nav.reviewTypeAheadValues")}</Label>
						<CountBadge count={typeAheadValues} />
					</Link>
					{role === "administrator" && (
						<>
							<Link role="menuitem" to="/admin/questions" onClick={selectItem} className={stacked ? stackedLinkClassName : rowLinkClassName}>
								<Label>{t("nav.manageQuestions")}</Label>
							</Link>
							<Link role="menuitem" to="/admin/answer-translations" onClick={selectItem} className={stacked ? stackedLinkClassName : rowLinkClassName}>
								<Label>{t("nav.manageAnswerTranslations")}</Label>
								<CountBadge count={translations} />
							</Link>
						</>
					)}
				</div>
			)}
		</div>
	)
}
