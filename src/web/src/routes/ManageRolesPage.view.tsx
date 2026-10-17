import type { FormEvent } from "react"
import { useLocale } from "../i18n/useLocale"
import type { AssignableRole, RoleAssignment } from "../api/roleAssignments"

const primaryButton =
	"touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600 disabled:opacity-40"
const secondaryButton =
	"touch-target inline-flex items-center rounded border border-rule px-5 font-sans text-ink hover:bg-surface-2 disabled:opacity-40"
const fieldClass = "touch-target mt-1 block w-full rounded border border-rule bg-surface px-3 font-sans text-ink"

export interface ManageRolesPageViewProps {
	loading: boolean
	error: string | null
	notice: string | null
	superAdministratorEmail: string | null
	assignments: RoleAssignment[]
	unavailable: boolean
	canChange: boolean
	assignableRoles: readonly AssignableRole[]
	email: string
	role: AssignableRole
	adding: boolean
	confirming: string | null
	busyId: string | null
	formatGranted: (value: string) => string
	changeEmail: (value: string) => void
	changeRole: (value: AssignableRole) => void
	submitAdd: (event: FormEvent) => void
	askRemove: (id: string) => void
	keep: () => void
	confirmRemove: (id: string) => void
	retry: () => void
}

export function ManageRolesPageView({
	loading,
	error,
	notice,
	superAdministratorEmail,
	assignments,
	unavailable,
	assignableRoles,
	canChange,
	email,
	role,
	adding,
	confirming,
	busyId,
	formatGranted,
	changeEmail,
	changeRole,
	submitAdd,
	askRemove,
	keep,
	confirmRemove,
	retry,
}: ManageRolesPageViewProps) {
	const { t } = useLocale()

	return (
		<main className="mx-auto max-w-4xl px-6 py-12">
			<h1 className="font-display text-3xl font-bold">{t("roles.title")}</h1>
			<p className="mt-2 font-sans text-ink-muted">{t("roles.intro")}</p>

			{error && (
				<p role="alert" className="mt-6 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{error}
					{!canChange && !loading && (
						<button type="button" className={`${secondaryButton} ml-4`} onClick={retry}>
							{t("list.retry")}
						</button>
					)}
				</p>
			)}
			<p role="status" className={notice ? "mt-6 font-sans text-ink" : "sr-only"}>
				{notice}
			</p>

			{canChange && (
				<form onSubmit={submitAdd} className="mt-8 rounded border border-rule p-4">
					<h2 className="font-display text-xl font-bold">{t("roles.add.title")}</h2>
					<div className="mt-4 grid gap-4 sm:grid-cols-2">
						<div>
							<label htmlFor="role-email" className="font-sans text-sm font-medium text-ink">
								{t("roles.add.email")}
							</label>
							<input
								id="role-email"
								type="email"
								required
								autoComplete="off"
								value={email}
								onChange={(event) => changeEmail(event.target.value)}
								className={fieldClass}
							/>
						</div>
						<div>
							<label htmlFor="role-role" className="font-sans text-sm font-medium text-ink">
								{t("roles.add.role")}
							</label>
							<select
								id="role-role"
								value={role}
								onChange={(event) => changeRole(event.target.value as AssignableRole)}
								className={fieldClass}
							>
								{assignableRoles.map((code) => (
									<option key={code} value={code}>
										{t(`roles.role.${code}`)}
									</option>
								))}
							</select>
						</div>
					</div>
					<button type="submit" className={`${primaryButton} mt-4`} disabled={adding}>
						{adding ? t("roles.add.working") : t("roles.add.submit")}
					</button>
				</form>
			)}

			<h2 className="mt-12 font-display text-2xl font-bold">{t("roles.listTitle")}</h2>

			{loading ? (
				<p className="mt-4 font-sans text-ink-muted">{t("roles.loading")}</p>
			) : (
				<>
					{unavailable && (
						<p role="alert" className="mt-4 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
							{t("roles.unavailable")}
						</p>
					)}
					<ul className="mt-4 divide-y divide-rule border-y border-rule">
						{superAdministratorEmail && (
							<li className="py-4 font-sans">
								<p className="font-medium text-ink">{superAdministratorEmail}</p>
								<p className="text-sm text-ink-muted">
									<strong>{t("roles.superAdministrator.label")}</strong> · {t("roles.superAdministrator.permanent")}
								</p>
							</li>
						)}
						{assignments.map((row) => (
							<li key={row.id} className="py-4 font-sans">
								<p className="font-medium text-ink">
									{row.email}
									{row.isSelf && <span className="ml-2 text-sm text-ink-muted">({t("roles.you")})</span>}
								</p>
								<p className="text-sm text-ink-muted">
									{t(`roles.role.${row.role}`)} · {formatGranted(row.grantedAt)}
								</p>
								{canChange && !row.isSelf && confirming !== row.id && (
									<button
										type="button"
										className={`${secondaryButton} mt-2`}
										disabled={busyId === row.id}
										aria-label={t("roles.remove.actionFor", { email: row.email })}
										onClick={() => askRemove(row.id)}
									>
										{t("roles.remove.action")}
									</button>
								)}
								{canChange && confirming === row.id && (
									<div role="group" aria-label={t("roles.remove.actionFor", { email: row.email })} className="mt-2">
										<p className="text-ink">{t("roles.remove.confirm", { email: row.email })}</p>
										<div className="mt-2 flex flex-wrap gap-3">
											<button type="button" className={primaryButton} onClick={() => confirmRemove(row.id)}>
												{t("roles.remove.yes")}
											</button>
											<button type="button" className={secondaryButton} onClick={keep}>
												{t("roles.remove.keep")}
											</button>
										</div>
									</div>
								)}
							</li>
						))}
					</ul>
					{!unavailable && assignments.length === 0 && (
						<p className="mt-4 font-sans text-ink-muted">{t("roles.empty")}</p>
					)}
				</>
			)}
		</main>
	)
}
