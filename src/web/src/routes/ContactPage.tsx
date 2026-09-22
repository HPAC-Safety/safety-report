import { useLocale } from "../i18n/useLocale"

const FACEBOOK_URL = "https://www.facebook.com/groups/1203572566353229/"
const YOUTUBE_URL = "https://www.youtube.com/c/HPACca"
const WHATSAPP_URL = "https://chat.whatsapp.com/KKV8tERQaNkKwHfqjkd0JX"

export function ContactPage() {
	const { t } = useLocale()

	return (
		<main className="mx-auto max-w-measure px-6 py-16">
			<h1 className="font-display text-3xl font-bold">{t("page.contact.title")}</h1>
			<p className="mt-2 font-sans text-ink-muted">{t("page.contact.orgName")}</p>

			<dl className="mt-8 space-y-6 font-sans">
				<div>
					<dt className="text-sm font-semibold text-ink">{t("page.contact.addressLabel")}</dt>
					<dd className="mt-1 whitespace-pre-line text-ink-muted">{t("page.contact.address")}</dd>
				</div>
				<div>
					<dt className="text-sm font-semibold text-ink">{t("page.contact.emailLabel")}</dt>
					<dd className="mt-1">
						<a href={`mailto:${t("page.contact.email")}`} className="text-brand-700 underline-offset-4 hover:underline">
							{t("page.contact.email")}
						</a>
					</dd>
				</div>
			</dl>

			<div className="mt-10">
				<h2 className="font-sans text-sm font-semibold text-ink">{t("page.contact.socialLabel")}</h2>
				<div className="mt-2 flex gap-4 font-sans">
					<a
						href={FACEBOOK_URL}
						target="_blank"
						rel="noreferrer"
						className="touch-target inline-flex items-center text-brand-700 underline-offset-4 hover:underline"
					>
						{t("page.contact.facebookLabel")}
					</a>
					<a
						href={YOUTUBE_URL}
						target="_blank"
						rel="noreferrer"
						className="touch-target inline-flex items-center text-brand-700 underline-offset-4 hover:underline"
					>
						{t("page.contact.youtubeLabel")}
					</a>
					<a
						href={WHATSAPP_URL}
						target="_blank"
						rel="noreferrer"
						className="touch-target inline-flex items-center text-brand-700 underline-offset-4 hover:underline"
					>
						{t("page.contact.whatsappLabel")}
					</a>
				</div>
			</div>
		</main>
	)
}
