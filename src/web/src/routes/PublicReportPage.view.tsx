import type { ComponentProps } from "react"
import { Link } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { Markdown } from "../components/Markdown"
import { AttachmentStrip } from "../components/AttachmentStrip"
import { ReportComments } from "../components/ReportComments"
import { OwnReportPill } from "../components/OwnReportPill"

type StripProps = ComponentProps<typeof AttachmentStrip>

/** What the page shows of one published report; the summary is already in the site's language. */
export interface PublicReportShown {
	id: string
	publishedAt: string
	language: "en-CA" | "fr-CA"
	media: StripProps["media"]
	staffAttachments: StripProps["staffAttachments"]
}

/** What the page shows of the visitor's own report before it is published. */
export interface OwnReportShown {
	id: string
	submittedAt: string
	forPublication: boolean
	language: "en-CA" | "fr-CA"
	media: StripProps["media"]
}

export type PublicReportLoaded =
	| { state: "loading" }
	| { state: "ready"; report: PublicReportShown; summary: string }
	| { state: "own"; report: OwnReportShown; receipt: string; summary: string | null }
	| { state: "missing" }
	| { state: "failed" }

export interface PublicReportPageViewProps {
	loaded: PublicReportLoaded
	isReviewer: boolean
	onChanged: () => void
}

export function PublicReportPageView({ loaded, isReviewer, onChanged }: PublicReportPageViewProps) {
	const { t, locale } = useLocale()
	const published = new Intl.DateTimeFormat(locale, { dateStyle: "long" })

	return (
		<main className="mx-auto max-w-measure px-6 py-12">
			<Link to="/reports" className="font-sans text-sm text-ink underline">
				{t("feed.back")}
			</Link>

			{loaded.state === "loading" && <p className="mt-8 font-sans text-ink-muted">{t("feed.loadingOne")}</p>}

			{loaded.state === "failed" && (
				<p role="alert" className="mt-8 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{t("feed.errorOne")}
				</p>
			)}

			{loaded.state === "missing" && (
				<>
					<h1 className="mt-4 font-display text-3xl font-bold">{t("feed.notFound.title")}</h1>
					<p className="mt-4 font-sans text-ink-muted">{t("feed.notFound.body")}</p>
				</>
			)}

			{loaded.state === "own" && (
				<article data-own-report={loaded.report.id}>
					<h1 className="mt-4 font-display text-3xl font-bold">{t("feed.reportTitle")}</h1>
					<p className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 font-sans text-sm text-ink-muted">
						<OwnReportPill forPublication={loaded.report.forPublication} />
						<span>{t("feed.own.submittedAt", { at: published.format(new Date(loaded.report.submittedAt)) })}</span>
					</p>
					{loaded.report.forPublication && loaded.report.language !== locale && loaded.summary !== null && (
						<p className="mt-1 font-sans text-sm text-ink-muted" data-translated-from={loaded.report.language}>
							{t(`feed.translatedFrom.${loaded.report.language}`)}
						</p>
					)}
					{!loaded.report.forPublication ? (
						<p className="mt-6 font-sans text-ink-muted">{t("feed.own.notForPublicationNote")}</p>
					) : loaded.summary === null ? (
						<p className="mt-6 font-sans text-ink-muted">{t("feed.own.summaryPending")}</p>
					) : (
						<>
							<p className="mt-6 font-sans text-sm text-ink-muted" data-draft-note>
								{t("feed.own.draftNote")}
							</p>
							<Markdown lang={locale} data-summary={locale} className="mt-2 font-sans text-ink-muted">
								{loaded.summary}
							</Markdown>
						</>
					)}
					<AttachmentStrip
						reportId={loaded.report.id}
						media={loaded.report.media}
						staffAttachments={null}
						receipt={loaded.receipt}
						onChanged={onChanged}
					/>
				</article>
			)}

			{loaded.state === "ready" && (
				<article>
					<h1 className="mt-4 font-display text-3xl font-bold">{t("feed.reportTitle")}</h1>
					<p className="mt-2 font-sans text-sm text-ink-muted">
						{t("feed.publishedAt", { at: published.format(new Date(loaded.report.publishedAt)) })}
					</p>
					{loaded.report.language !== locale && (
						<p className="mt-1 font-sans text-sm text-ink-muted" data-translated-from={loaded.report.language}>
							{t(`feed.translatedFrom.${loaded.report.language}`)}
						</p>
					)}
					{isReviewer && (
						<Link
							to={`/admin/reports/${loaded.report.id}`}
							className="mt-1 inline-block font-sans text-sm text-ink underline"
							data-admin-link
						>
							{t("feed.adminPage")}
						</Link>
					)}
					<Markdown lang={locale} data-summary={locale} className="mt-6 font-sans text-ink-muted">
						{loaded.summary}
					</Markdown>
					<AttachmentStrip
						reportId={loaded.report.id}
						media={loaded.report.media}
						staffAttachments={loaded.report.staffAttachments}
						onChanged={onChanged}
					/>
					<ReportComments reportId={loaded.report.id} />
				</article>
			)}
		</main>
	)
}
