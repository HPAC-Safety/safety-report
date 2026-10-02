import type { RefObject } from "react"
import { Link } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { CountBadge } from "./CountBadge"

const rowLinkClassName =
	"group touch-target flex items-center whitespace-nowrap rounded px-4 font-sans text-sm font-medium text-ink"

const stackedLinkClassName =
	"group touch-target flex items-center rounded px-2 font-sans text-base font-medium text-ink"

/** Underlines only the words on hover, never the count beside them. */
function Label({ children }: { children: string }) {
	return <span className="underline-offset-4 group-hover:underline">{children}</span>
}

export interface AdminMenuViewProps {
	stacked?: boolean
	open: boolean
	/** Reports needing action; zero shows no badge. */
	reports: number
	/** Type-ahead values awaiting review; zero shows no badge. */
	typeAheadValues: number
	isAdministrator: boolean
	containerRef: RefObject<HTMLDivElement>
	buttonRef: RefObject<HTMLButtonElement>
	onToggle: () => void
	onSelectItem: () => void
}

export function AdminMenuView({
	stacked = false,
	open,
	reports,
	typeAheadValues,
	isAdministrator,
	containerRef,
	buttonRef,
	onToggle,
	onSelectItem,
}: AdminMenuViewProps) {
	const { t } = useLocale()

	return (
		<div ref={containerRef} className="relative">
			<button
				ref={buttonRef}
				type="button"
				onClick={onToggle}
				aria-haspopup="menu"
				aria-expanded={open}
				className={stacked ? stackedLinkClassName : "group touch-target inline-flex items-center rounded px-2 font-sans text-sm font-medium text-ink"}
			>
				<Label>{t("nav.admin")}</Label>
				<CountBadge count={reports + typeAheadValues} />
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
					<Link role="menuitem" to="/admin/reports" onClick={onSelectItem} className={stacked ? stackedLinkClassName : rowLinkClassName}>
						<Label>{t("nav.manageReports")}</Label>
						<CountBadge count={reports} />
					</Link>
					<Link role="menuitem" to="/admin/type-ahead-values" onClick={onSelectItem} className={stacked ? stackedLinkClassName : rowLinkClassName}>
						<Label>{t("nav.reviewTypeAheadValues")}</Label>
						<CountBadge count={typeAheadValues} />
					</Link>
					{isAdministrator && (
						<>
							<Link role="menuitem" to="/admin/questions" onClick={onSelectItem} className={stacked ? stackedLinkClassName : rowLinkClassName}>
								<Label>{t("nav.manageQuestions")}</Label>
							</Link>
						</>
					)}
				</div>
			)}
		</div>
	)
}
