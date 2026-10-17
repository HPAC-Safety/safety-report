import { clearSession } from "../auth/session"
import { authorization } from "./adminQuestions"

/*
 * The role-assignment client (REQ-AUTH-012..029). Every call carries the
 * member's bearer token; the API authorizes each one itself (ADR-0048).
 * An email appears only in these requests and responses, never in a URL, so
 * it cannot reach an access log (CON-IF-011).
 */

/** The roles an Administrator may assign, as the API's role codes. */
export const ASSIGNABLE_ROLES = ["safety_officer", "administrator"] as const

export type AssignableRole = (typeof ASSIGNABLE_ROLES)[number]

export interface RoleAssignment {
	id: string
	email: string
	role: AssignableRole
	grantedAt: string
	/** True on the caller's own assignment, which cannot be removed here. */
	isSelf?: boolean
}

export interface RoleAssignmentList {
	/** The permanent Super Administrator from configuration, or null when none is configured. */
	superAdministrator: { email: string } | null
	/** Null while the store cannot be read. */
	assignments: RoleAssignment[] | null
	assignmentsUnavailable: boolean
}

/** Why a change was refused, as a stable code; never the API's prose (it may echo input and is English only). */
export type RefusalCode = "duplicate" | "super-administrator" | "self" | "not-found" | "invalid-email" | "invalid-role" | "other"

const KNOWN_CODES: readonly string[] = ["duplicate", "super-administrator", "self", "not-found", "invalid-email", "invalid-role"]

export class RoleAssignmentError extends Error {
	constructor(
		readonly status: number,
		readonly code: RefusalCode,
	) {
		super(code)
		this.name = "RoleAssignmentError"
	}
}

interface Problem {
	code?: unknown
}

function refusalCode(status: number, problem: Problem | null): RefusalCode {
	if (typeof problem?.code === "string" && KNOWN_CODES.includes(problem.code)) return problem.code as RefusalCode
	return status === 404 ? "not-found" : "other"
}

// Signature split across lines on purpose: tools/web/check-hardcoded-strings.ts
// is a line scanner — see adminQuestions.ts.
async function call<T>(
	path: string,
	init?: RequestInit,
): Promise<T> {
	const response = await fetch(path, {
		...init,
		// eslint-disable-next-line @typescript-eslint/no-misused-spread -- every caller passes headers as a plain record
		headers: { "Content-Type": "application/json", ...authorization(), ...init?.headers },
	})

	if (response.status === 401) clearSession()

	if (!response.ok) {
		const problem = (await response.json().catch(() => null)) as Problem | null
		throw new RoleAssignmentError(response.status, refusalCode(response.status, problem))
	}

	return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

export function listRoleAssignments(): Promise<RoleAssignmentList> {
	return call<RoleAssignmentList>("/api/admin/role-assignments")
}

export function addRoleAssignment(email: string, role: AssignableRole): Promise<RoleAssignment> {
	return call<RoleAssignment>("/api/admin/role-assignments", {
		method: "POST",
		body: JSON.stringify({ email, role }),
	})
}

export function revokeRoleAssignment(id: string): Promise<void> {
	return call<undefined>(`/api/admin/role-assignments/${encodeURIComponent(id)}/revoke`, { method: "POST" })
}
