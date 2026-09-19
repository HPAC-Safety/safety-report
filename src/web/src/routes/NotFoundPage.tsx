import { useLocale } from "../i18n/useLocale"

export function NotFoundPage() {
	const { t } = useLocale()

	return (
		<main className="mx-auto max-w-measure px-6 py-16">
			<h1 className="font-display text-3xl font-bold">{t("notFound.title")}</h1>
			<p className="mt-4 font-sans text-ink-muted">{t("notFound.body")}</p>
		</main>
	)
}
