import { Link } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"

function Section({ title, body, tone }: { title: string; body: string; tone: "surface" | "surface-2" }) {
	return (
		<section className={tone === "surface" ? "bg-surface" : "bg-surface-2"}>
			<div className="mx-auto max-w-5xl px-6 py-12">
				<h2 className="font-display text-2xl font-bold">{title}</h2>
				<p className="mt-3 max-w-measure font-sans text-ink-muted">{body}</p>
			</div>
		</section>
	)
}

export function HomePage() {
	const { t } = useLocale()

	return (
		<main>
			<section className="bg-surface-3">
				<div className="mx-auto max-w-5xl px-6 py-16 text-center">
					<h1 className="font-display text-4xl font-bold">{t("home.heroTitle")}</h1>
					<p className="mx-auto mt-4 max-w-measure font-sans text-lg text-ink-muted">{t("home.heroSubtitle")}</p>
					<Link
						to="/report"
						className="touch-target mt-8 inline-flex items-center rounded bg-brand-700 px-6 font-sans font-semibold text-ink-inverse"
					>
						{t("home.heroCta")}
					</Link>
				</div>
			</section>

			<Section title={t("home.sectionWhy.title")} body={t("home.sectionWhy.body")} tone="surface" />
			<Section title={t("home.sectionHow.title")} body={t("home.sectionHow.body")} tone="surface-2" />
			<Section title={t("home.sectionAbout.title")} body={t("home.sectionAbout.body")} tone="surface" />
		</main>
	)
}
