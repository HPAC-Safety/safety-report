import type { ReportListItem } from "../api/adminReports"
import { rowActionsFor, type RowAction } from "./rowActions"
import { ReportRowActionsView } from "./ReportRowActions.view"

export { rowActionsFor, type RowAction }

export interface ReportRowActionsProps {
	report: ReportListItem
	label: string
	busy: boolean
	onAction: (action: RowAction) => void
}

/** The view model: which quick actions the report's row offers. */
export function useReportRowActions({ report }: ReportRowActionsProps) {
	return { actions: rowActionsFor(report) }
}

/*
 * Icon buttons beside a report in the list. Each carries its action's name, so
 * a screen reader and the tooltip both say what it does; the group names the
 * report it acts on.
 */
export function ReportRowActions(props: ReportRowActionsProps) {
	return <ReportRowActionsView {...props} {...useReportRowActions(props)} />
}
