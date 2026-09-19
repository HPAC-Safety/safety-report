import { useLocale } from "../i18n/useLocale"

/** Shared shell for every dummy nav destination in this spike — no real content yet. */
export function PlaceholderPage({ pageName }: { pageName: string }) {
	const { t } = useLocale()

	return (
		<main className="mx-auto max-w-measure px-6 py-16">
			<h1 className="font-display text-3xl font-bold">{t("page.placeholder.title", { pageName })}</h1>
			<p className="mt-4 font-sans text-ink-muted">{t("page.placeholder.body", { pageName })}</p>
		</main>
	)
}
