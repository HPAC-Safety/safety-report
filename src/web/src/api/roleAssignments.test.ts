import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"

vi.mock("../auth/session", () => ({
	readSession: vi.fn(() => ({ accessToken: "tok" })),
	clearSession: vi.fn(),
}))

import { clearSession } from "../auth/session"
import { addRoleAssignment, listRoleAssignments, revokeRoleAssignment, RoleAssignmentError } from "./roleAssignments"

const fetchMock = vi.fn<(path: string, init: RequestInit & { headers: Record<string, string> }) => Promise<unknown>>()

function reply(status: number, body: unknown = {}): Response {
	return {
		status,
		statusText: "Text",
		ok: status >= 200 && status < 300,
		json: () => Promise.resolve(body),
	} as unknown as Response
}

beforeEach(() => {
	vi.stubGlobal("fetch", fetchMock)
})
afterEach(() => {
	fetchMock.mockReset()
	vi.mocked(clearSession).mockClear()
	vi.unstubAllGlobals()
})

describe("role assignments client", () => {
	it("lists with the bearer token", async () => {
		const body = { superAdministrator: null, assignments: [], assignmentsUnavailable: false }
		fetchMock.mockResolvedValue(reply(200, body))

		await expect(listRoleAssignments()).resolves.toEqual(body)

		expect(fetchMock.mock.calls[0][0]).toBe("/api/admin/role-assignments")
		expect(fetchMock.mock.calls[0][1].headers.Authorization).toBe("Bearer tok")
	})

	it("sends the email in the body, never the address", async () => {
		fetchMock.mockResolvedValue(reply(200, { id: "1" }))

		await addRoleAssignment("a@example.com", "safety_officer")

		const [path, init] = fetchMock.mock.calls[0]
		expect(path).toBe("/api/admin/role-assignments")
		expect(init.method).toBe("POST")
		expect(init.body).toBe(JSON.stringify({ email: "a@example.com", role: "safety_officer" }))
	})

	it("revokes by id and reads 204 as nothing", async () => {
		fetchMock.mockResolvedValue(reply(204))

		await expect(revokeRoleAssignment("abc")).resolves.toBeUndefined()

		expect(fetchMock.mock.calls[0][0]).toBe("/api/admin/role-assignments/abc/revoke")
		expect(fetchMock.mock.calls[0][1].method).toBe("POST")
	})

	it("throws the refusal code, never the prose", async () => {
		fetchMock.mockResolvedValue(reply(400, { code: "duplicate", detail: "a@example.com is taken" }))

		const error = await addRoleAssignment("a@example.com", "administrator").catch((cause: unknown) => cause)

		expect(error).toBeInstanceOf(RoleAssignmentError)
		expect(error).toMatchObject({ status: 400, code: "duplicate", message: "duplicate" })
	})

	it("maps a 404 with no code to not-found and anything else to other", async () => {
		fetchMock.mockResolvedValueOnce(reply(404, {})).mockResolvedValueOnce(reply(400, { code: "surprise" }))

		await expect(revokeRoleAssignment("x")).rejects.toMatchObject({ code: "not-found" })
		await expect(addRoleAssignment("a@example.com", "administrator")).rejects.toMatchObject({ code: "other" })
	})

	it("clears the session on 401", async () => {
		fetchMock.mockResolvedValue(reply(401, null))

		await expect(listRoleAssignments()).rejects.toMatchObject({ status: 401, code: "other" })
		expect(clearSession).toHaveBeenCalled()
	})

	it("maps an error body that is not JSON to the status fallback", async () => {
		const broken = (status: number) =>
			({ status, ok: false, json: () => Promise.reject(new Error("not json")) }) as unknown as Response
		fetchMock.mockResolvedValueOnce(broken(404))
		await expect(listRoleAssignments()).rejects.toMatchObject({ status: 404, code: "not-found" })

		fetchMock.mockResolvedValueOnce(broken(500))
		await expect(listRoleAssignments()).rejects.toMatchObject({ status: 500, code: "other" })
	})
})
