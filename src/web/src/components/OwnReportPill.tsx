import { OwnReportPillView, type OwnReportPillViewProps } from "./OwnReportPill.view"

/** A markup-only component: the pass-through the split convention asks for (ADR-0188). */
export function OwnReportPill(props: OwnReportPillViewProps) {
	return <OwnReportPillView {...props} />
}
