import type { ReactNode } from "react"
import { useLocale } from "../i18n/useLocale"
import type { RowAction } from "./rowActions"

const BUTTON =
	"touch-target inline-flex items-center justify-center rounded border border-rule px-3 text-ink hover:bg-surface-2 disabled:opacity-50"

/*
 * Icon buttons beside a report in the list. Each carries its action's name, so
 * a screen reader and the tooltip both say what it does; the group names the
 * report it acts on.
 */
export interface ReportRowActionsViewProps {
	label: string
	busy: boolean
	onAction: (action: RowAction) => void
	actions: RowAction[]
}

export function ReportRowActionsView({ label, busy, onAction, actions }: ReportRowActionsViewProps) {
	const { t } = useLocale()

	return (
		<div role="group" aria-label={label} className="flex shrink-0 gap-2">
			{actions.map((action) => (
				<button
					key={action}
					type="button"
					data-row-action={action}
					aria-label={t(`reports.action.${action}`)}
					title={t(`reports.action.${action}`)}
					disabled={busy}
					onClick={() => onAction(action)}
					className={action === "delete" ? `${BUTTON} hover:text-brand-700` : BUTTON}
				>
					{ICONS[action]}
				</button>
			))}
		</div>
	)
}

function Icon({ children }: { children: ReactNode }) {
	return (
		<svg
			viewBox="0 0 24 24"
			width="20"
			height="20"
			fill="none"
			stroke="currentColor"
			strokeWidth="2"
			strokeLinecap="round"
			strokeLinejoin="round"
			aria-hidden="true"
		>
			{children}
		</svg>
	)
}

const ICONS: Record<RowAction, ReactNode> = {
	// A globe: make it public.
	publish: (
		<Icon>
			<circle cx="12" cy="12" r="10" />
			<path d="M2 12h20" />
			<path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
		</Icon>
	),
	// A crossed-out eye: take it off the public feed.
	unpublish: (
		<Icon>
			<path d="M9.88 9.88a3 3 0 1 0 4.24 4.24" />
			<path d="M10.73 5.08A10.43 10.43 0 0 1 12 5c7 0 10 7 10 7a13.16 13.16 0 0 1-1.67 2.68" />
			<path d="M6.61 6.61A13.53 13.53 0 0 0 2 12s3 7 10 7a9.74 9.74 0 0 0 5.39-1.61" />
			<path d="M2 2l20 20" />
		</Icon>
	),
	// A bin.
	delete: (
		<Icon>
			<path d="M3 6h18" />
			<path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6" />
			<path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
			<path d="M10 11v6" />
			<path d="M14 11v6" />
		</Icon>
	),
}
