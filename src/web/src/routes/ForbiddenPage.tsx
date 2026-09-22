import { useLocale } from "../i18n/useLocale"

/** Shown in place of an admin route's content when the signed-in member's role cannot use it (ADR-0092). */
export function ForbiddenPage() {
	const { t } = useLocale()

	return (
		<main className="mx-auto max-w-measure px-6 py-16">
			<h1 className="font-display text-3xl font-bold">{t("forbidden.title")}</h1>
			<p className="mt-4 font-sans text-ink-muted">{t("forbidden.body")}</p>
		</main>
	)
}
