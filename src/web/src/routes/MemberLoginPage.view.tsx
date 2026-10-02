import type { FormEvent } from "react"
import { useLocale } from "../i18n/useLocale"

export interface MemberLoginPageViewProps {
	username: string
	password: string
	submitting: boolean
	failed: boolean
	thirdPartySignIn: boolean
	onUsernameChange: (value: string) => void
	onPasswordChange: (value: string) => void
	onSubmit: (event: FormEvent) => void
}

export function MemberLoginPageView({
	username,
	password,
	submitting,
	failed,
	thirdPartySignIn,
	onUsernameChange,
	onPasswordChange,
	onSubmit,
}: MemberLoginPageViewProps) {
	const { t } = useLocale()

	return (
		<main className="mx-auto max-w-measure px-6 py-16">
			<h1 className="font-display text-3xl font-bold">{t("page.login.title")}</h1>

			{/* noValidate: the field is type="email" for a phone's email keyboard, but
			    Development's fixed accounts (ADR-0066) sign in as plain "admin" and
			    the like, which the browser would otherwise refuse. */}
			<form className="mt-8 flex flex-col gap-4" onSubmit={onSubmit} noValidate>
				{failed && (
					<p role="alert" className="rounded border border-rule bg-surface px-3 py-2 font-sans text-sm text-ink">
						{t("page.login.errorGeneric")}
					</p>
				)}

				<label className="flex flex-col gap-1 font-sans text-sm text-ink">
					{t("page.login.usernameLabel")}
					<input
						type="email"
						name="username"
						value={username}
						onChange={(event) => onUsernameChange(event.target.value)}
						autoComplete="email"
						autoCapitalize="none"
						autoCorrect="off"
						spellCheck={false}
						className="touch-target rounded border border-rule bg-surface px-3 text-ink"
					/>
				</label>

				<label className="flex flex-col gap-1 font-sans text-sm text-ink">
					{t("page.login.passwordLabel")}
					<input
						type="password"
						name="password"
						value={password}
						onChange={(event) => onPasswordChange(event.target.value)}
						autoComplete="current-password"
						className="touch-target rounded border border-rule bg-surface px-3 text-ink"
					/>
				</label>

				{thirdPartySignIn && (
					<button
						type="button"
						className="touch-target mt-2 inline-flex items-center justify-center rounded border border-rule bg-surface px-4 font-sans text-sm font-semibold text-ink"
					>
						{t("page.login.googleButton")}
					</button>
				)}

				<button
					type="submit"
					disabled={submitting}
					className="touch-target inline-flex items-center justify-center rounded bg-brand-700 px-4 font-sans text-sm font-semibold text-ink-inverse disabled:opacity-60"
				>
					{submitting ? t("page.login.submitting") : t("page.login.submitButton")}
				</button>
			</form>
		</main>
	)
}
