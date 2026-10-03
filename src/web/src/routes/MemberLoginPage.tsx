import { useEffect, useState, type FormEvent } from "react"
import { useNavigate, useSearchParams } from "react-router-dom"
import { useAuth } from "../auth/useAuth"
import { loadAuthConfig } from "../auth/authApi"
import { MemberLoginPageView } from "./MemberLoginPage.view"
import { returnTarget } from "./returnTarget"

export function useMemberLoginPage() {
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

		void loadAuthConfig().then((config) => {
			if (!cancelled) setThirdPartySignIn(config.thirdPartySignIn)
		})

		return () => {
			cancelled = true
		}
	}, [])

	async function onSubmit(event: FormEvent) {
		event.preventDefault()

		setSubmitting(true)
		setFailed(false)

		try {
			await signInWithPassword(username, password)
			void navigate(returnTarget(searchParams.get("returnTo")))
		} catch {
			// One message for every reason, matching what the API returns.
			setFailed(true)
		} finally {
			setSubmitting(false)
		}
	}

	return {
		username,
		password,
		submitting,
		failed,
		thirdPartySignIn,
		onUsernameChange: setUsername,
		onPasswordChange: setPassword,
		onSubmit,
	}
}

/**
 * Member sign-in.
 *
 * Whether a third-party option is offered comes from the API, not a build
 * flag, and the button is hidden rather than disabled where none is
 * configured — a disabled control still reads to a screen reader as something
 * on offer. See ADR-0066.
 */
export function MemberLoginPage() {
	return <MemberLoginPageView {...useMemberLoginPage()} />
}
