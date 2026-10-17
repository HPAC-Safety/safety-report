import { useEffect, useState, type FormEvent } from "react"
import { useLocale } from "../i18n/useLocale"
import {
	ASSIGNABLE_ROLES,
	addRoleAssignment,
	listRoleAssignments,
	RoleAssignmentError,
	revokeRoleAssignment,
	type AssignableRole,
	type RoleAssignmentList,
} from "../api/roleAssignments"
import { ManageRolesPageView } from "./ManageRolesPage.view"

/*
 * The manage-roles page (REQ-AUTH-012..019, 029). It lists the permanent
 * Super Administrator first, with no remove control, then each active role
 * assignment. When the store cannot be read it says so and offers no add or
 * remove (REQ-AUTH-029). Removing asks inline first, never with a browser
 * dialog, and the caller's own row offers no removal.
 */
export function useManageRolesPage() {
	const { t, locale } = useLocale()
	const [data, setData] = useState<RoleAssignmentList | null>(null)
	const [loading, setLoading] = useState(true)
	const [error, setError] = useState<string | null>(null)
	const [notice, setNotice] = useState<string | null>(null)
	const [email, setEmail] = useState("")
	const [role, setRole] = useState<AssignableRole>("safety_officer")
	const [adding, setAdding] = useState(false)
	const [confirming, setConfirming] = useState<string | null>(null)
	const [busyId, setBusyId] = useState<string | null>(null)

	async function load() {
		setLoading(true)
		setError(null)
		try {
			setData(await listRoleAssignments())
		} catch {
			setData(null)
			setError(t("roles.error.load"))
		} finally {
			setLoading(false)
		}
	}

	useEffect(() => {
		void load()
		// eslint-disable-next-line react-hooks/exhaustive-deps -- load once on mount
	}, [])

	function refusal(cause: unknown, fallback: string) {
		if (!(cause instanceof RoleAssignmentError) || cause.code === "other") return fallback
		return t(`roles.refused.${cause.code}`)
	}

	async function add(event: FormEvent) {
		event.preventDefault()
		const trimmed = email.trim()
		if (!trimmed) return
		setAdding(true)
		setError(null)
		setNotice(null)
		try {
			const created = await addRoleAssignment(trimmed, role)
			setData((current) =>
				current && current.assignments ? { ...current, assignments: [...current.assignments, created] } : current,
			)
			setEmail("")
			setNotice(t("roles.add.done"))
		} catch (cause) {
			setError(refusal(cause, t("roles.error.add")))
		} finally {
			setAdding(false)
		}
	}

	async function remove(id: string) {
		setConfirming(null)
		setBusyId(id)
		setError(null)
		setNotice(null)
		try {
			await revokeRoleAssignment(id)
			setData((current) =>
				current && current.assignments
					? { ...current, assignments: current.assignments.filter((row) => row.id !== id) }
					: current,
			)
			setNotice(t("roles.remove.done"))
		} catch (cause) {
			setError(refusal(cause, t("roles.error.remove")))
		} finally {
			setBusyId(null)
		}
	}

	const granted = new Intl.DateTimeFormat(locale, { dateStyle: "medium" })
	const unavailable = data !== null && (data.assignmentsUnavailable || data.assignments === null)

	return {
		assignableRoles: ASSIGNABLE_ROLES,
		loading,
		error,
		notice,
		superAdministratorEmail: data?.superAdministrator?.email ?? null,
		assignments: data?.assignments ?? [],
		unavailable,
		canChange: data !== null && !unavailable,
		email,
		role,
		adding,
		confirming,
		busyId,
		formatGranted: (value: string) => granted.format(new Date(value)),
		changeEmail: setEmail,
		changeRole: setRole,
		submitAdd: (event: FormEvent) => void add(event),
		askRemove: (id: string) => setConfirming(id),
		keep: () => setConfirming(null),
		confirmRemove: (id: string) => void remove(id),
		retry: () => void load(),
	}
}

export function ManageRolesPage() {
	return <ManageRolesPageView {...useManageRolesPage()} />
}
