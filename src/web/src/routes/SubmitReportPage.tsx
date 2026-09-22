import { Link } from "react-router-dom"

import { useAuth } from "../auth/useAuth"
import { useLocale } from "../i18n/useLocale"
import { ReportForm } from "../report-form/ReportForm"
import { ReportFormErrorBoundary } from "../report-form/ReportFormErrorBoundary"

/**
 * Filing a report requires a signed-in HPAC member, and records nothing about
 * them (ADR-0067).
 *
 * The notice is not decoration. An anonymity guarantee the reporter cannot see
 * is worth nothing, because the only thing that changes what somebody is
 * willing to write down is what they believe while they are typing it.
 */
export function SubmitReportPage() {
	const { t } = useLocale()
	const { status, isSignedIn } = useAuth()

	// The stored token is still being checked. Showing the sign-in prompt here
	// would make a signed-in member's own page flash a wall at them.
	if (status === "unknown") {
		return <main className="mx-auto max-w-measure px-6 py-16" aria-busy="true" />
	}

	if (!isSignedIn) {
		return (
			<main className="mx-auto max-w-measure px-6 py-16">
				<h1 className="font-display text-3xl font-bold">{t("page.report.signInRequiredTitle")}</h1>
				<p className="mt-4 font-sans text-ink-muted">{t("page.report.signInRequiredBody")}</p>
				<Link
					to="/login"
					className="touch-target mt-6 inline-flex items-center justify-center rounded bg-brand-700 px-4 font-sans text-sm font-semibold text-ink-inverse"
				>
					{t("page.report.signInAction")}
				</Link>
			</main>
		)
	}

	return (
		<main>
			<section className="mx-auto max-w-measure px-6 pt-16">
				<div className="rounded border border-rule bg-surface px-4 py-3">
					<h2 className="font-sans text-sm font-semibold text-ink">{t("page.report.notTrackedTitle")}</h2>
					<p className="mt-1 font-sans text-sm text-ink-muted">{t("page.report.notTrackedBody")}</p>
				</div>
			</section>

			<ReportFormErrorBoundary t={t}>
				<ReportForm />
			</ReportFormErrorBoundary>
		</main>
	)
}
