import { useNavigate } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { useAuth } from "../auth/useAuth"

// Mock OAuth-style screen — the fields and Google button are no-ops. See
// AuthContext for the fake session marker this stands in for until
// IMemberAuthenticator/OidcAuthenticator (ADR-0005) exists.
export function MemberLoginPage() {
	const { t } = useLocale()
	const { signIn } = useAuth()
	const navigate = useNavigate()

	function handleSignIn() {
		signIn()
		navigate("/")
	}

	return (
		<main className="mx-auto max-w-measure px-6 py-16">
			<h1 className="font-display text-3xl font-bold">{t("page.login.title")}</h1>

			<form
				className="mt-8 flex flex-col gap-4"
				onSubmit={(event) => {
					event.preventDefault()
					handleSignIn()
				}}
			>
				<label className="flex flex-col gap-1 font-sans text-sm text-ink">
					{t("page.login.usernameLabel")}
					<input type="text" name="username" className="touch-target rounded border border-rule bg-surface px-3 text-ink" />
				</label>

				<label className="flex flex-col gap-1 font-sans text-sm text-ink">
					{t("page.login.passwordLabel")}
					<input type="password" name="password" className="touch-target rounded border border-rule bg-surface px-3 text-ink" />
				</label>

				<button
					type="button"
					onClick={handleSignIn}
					className="touch-target mt-2 inline-flex items-center justify-center rounded border border-rule bg-surface px-4 font-sans text-sm font-semibold text-ink"
				>
					{t("page.login.googleButton")}
				</button>

				<button
					type="submit"
					className="touch-target inline-flex items-center justify-center rounded bg-brand-700 px-4 font-sans text-sm font-semibold text-ink-inverse"
				>
					{t("page.login.submitButton")}
				</button>
			</form>
		</main>
	)
}
