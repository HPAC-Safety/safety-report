import { useEffect, useState } from "react"
import { useNavigate, useSearchParams } from "react-router-dom"
import { useLocale } from "../i18n/useLocale"
import { useAuth } from "../auth/useAuth"
import { loadAuthConfig } from "../auth/authApi"

/**
 * Member sign-in.
 *
 * Whether a third-party option is offered comes from the API, not a build
 * flag, and the button is hidden rather than disabled where none is
 * configured — a disabled control still reads to a screen reader as something
 * on offer. See ADR-0066.
 */
export function MemberLoginPage() {
	const { t } = useLocale()
	const { signInWithPassword } = useAuth()
	const navigate = useNavigate()
	const [searchParams] = useSearchParams()

	const [username, setUsername] = useState("")
	const [password, setPassword] = useState("")
	const [submitting, setSubmitting] = useState(false)
	const [failed, setFailed] = useState(false)
	const [thirdPartySignIn, setThirdPartySignIn] = useState(false)

	useEffect(() => {
		let cancelled = false

		loadAuthConfig().then((config) => {
			if (!cancelled) setThirdPartySignIn(config.thirdPartySignIn)
		})

		return () => {
			cancelled = true
		}
	}, [])

	async function handleSubmit(event: React.FormEvent) {
		event.preventDefault()

		setSubmitting(true)
		setFailed(false)

		try {
			await signInWithPassword(username, password)
			navigate(returnTarget(searchParams.get("returnTo")))
		} catch {
			// One message for every reason, matching what the API returns.
			setFailed(true)
		} finally {
			setSubmitting(false)
		}
	}

	return (
		<main className="mx-auto max-w-measure px-6 py-16">
			<h1 className="font-display text-3xl font-bold">{t("page.login.title")}</h1>

			<form className="mt-8 flex flex-col gap-4" onSubmit={handleSubmit}>
				{failed && (
					<p role="alert" className="rounded border border-rule bg-surface px-3 py-2 font-sans text-sm text-ink">
						{t("page.login.errorGeneric")}
					</p>
				)}

				<label className="flex flex-col gap-1 font-sans text-sm text-ink">
					{t("page.login.usernameLabel")}
					<input
						type="text"
						name="username"
						value={username}
						onChange={(event) => setUsername(event.target.value)}
						autoComplete="username"
						className="touch-target rounded border border-rule bg-surface px-3 text-ink"
					/>
				</label>

				<label className="flex flex-col gap-1 font-sans text-sm text-ink">
					{t("page.login.passwordLabel")}
					<input
						type="password"
						name="password"
						value={password}
						onChange={(event) => setPassword(event.target.value)}
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

/**
 * Where to go after signing in: the page that sent the member here, such as a
 * report they wanted to comment on, but only a path on this site. Anything
 * else — another origin, a protocol-relative URL — goes home.
 */
function returnTarget(requested: string | null): string {
	return requested && requested.startsWith("/") && !requested.startsWith("//") && !requested.startsWith("/\\") ? requested : "/"
}
