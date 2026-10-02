import type { ReportFormErrorBoundaryProps } from "./ReportFormErrorBoundary"

export type ReportFormErrorBoundaryViewProps = Pick<ReportFormErrorBoundaryProps, "t">

/** What the form shows in place of itself when it fails to render. */
export function ReportFormErrorBoundaryView({ t }: ReportFormErrorBoundaryViewProps) {
	return (
		<p role="alert" className="mx-auto max-w-measure px-6 py-10 font-sans text-brand-700">
			{t("report.loadError")}
		</p>
	)
}
