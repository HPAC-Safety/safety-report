import { act, cleanup, render, renderHook, screen, waitFor } from "@testing-library/react"
import userEvent from "@testing-library/user-event"
import type { ReactNode } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import {
	addRoleAssignment,
	listRoleAssignments,
	revokeRoleAssignment,
	RoleAssignmentError,
	type RoleAssignment,
	type RoleAssignmentList,
} from "../api/roleAssignments"
import { LocaleContext } from "../i18n/LocaleProvider"
import { ManageRolesPage, useManageRolesPage } from "./ManageRolesPage"

vi.mock("../api/roleAssignments", async (importOriginal) => ({
	...(await importOriginal<typeof import("../api/roleAssignments")>()),
	listRoleAssignments: vi.fn(),
	addRoleAssignment: vi.fn(),
	revokeRoleAssignment: vi.fn(),
}))

const list = vi.mocked(listRoleAssignments)
const add = vi.mocked(addRoleAssignment)
const revoke = vi.mocked(revokeRoleAssignment)

function wrap({ children }: { children: ReactNode }) {
	const t = (key: string, params?: Record<string, string | number>) =>
		params ? `${key} ${Object.values(params).join(" ")}` : key
	return <LocaleContext.Provider value={{ locale: "en-CA", setLocale: () => {}, t }}>{children}</LocaleContext.Provider>
}

function assignment(id: string, over: Partial<RoleAssignment> = {}): RoleAssignment {
	return { id, email: `${id}@example.com`, role: "safety_officer", grantedAt: "2026-10-01T12:00:00Z", ...over }
}

function listing(over: Partial<RoleAssignmentList> = {}): RoleAssignmentList {
	return {
		superAdministrator: { email: "root@example.com" },
		assignments: [assignment("a"), assignment("me", { isSelf: true, role: "administrator" })],
		assignmentsUnavailable: false,
		...over,
	}
}

afterEach(cleanup)

beforeEach(() => {
	list.mockReset()
	add.mockReset()
	revoke.mockReset()
})

describe("ManageRolesPage", () => {
	it("lists the Super Administrator as permanent with no remove control", async () => {
		list.mockResolvedValue(listing({ assignments: [] }))
		render(<ManageRolesPage />, { wrapper: wrap })

		expect(await screen.findByText("root@example.com")).toBeTruthy()
		expect(screen.getByText(/roles.superAdministrator.permanent/)).toBeTruthy()
		expect(screen.queryByRole("button", { name: /roles.remove.actionFor/ })).toBeNull()
	})

	it("offers removal on other rows but not on the caller's own", async () => {
		list.mockResolvedValue(listing())
		render(<ManageRolesPage />, { wrapper: wrap })

		await screen.findByText("a@example.com")

		expect(screen.getAllByRole("button", { name: /roles.remove.actionFor/ })).toHaveLength(1)
		expect(screen.getByText("(roles.you)")).toBeTruthy()
	})

	it("confirms inline, then removes the row", async () => {
		list.mockResolvedValue(listing())
		revoke.mockResolvedValue(undefined)
		const user = userEvent.setup()
		render(<ManageRolesPage />, { wrapper: wrap })
		await screen.findByText("a@example.com")

		await user.click(screen.getByRole("button", { name: /roles.remove.actionFor/ }))
		expect(revoke).not.toHaveBeenCalled()
		await user.click(screen.getByRole("button", { name: "roles.remove.yes" }))

		await waitFor(() => expect(screen.queryByText("a@example.com")).toBeNull())
		expect(revoke).toHaveBeenCalledWith("a")
		expect(screen.getByRole("status").textContent).toBe("roles.remove.done")
	})

	it("keeps the row when the confirm is declined", async () => {
		list.mockResolvedValue(listing())
		const user = userEvent.setup()
		render(<ManageRolesPage />, { wrapper: wrap })
		await screen.findByText("a@example.com")

		await user.click(screen.getByRole("button", { name: /roles.remove.actionFor/ }))
		await user.click(screen.getByRole("button", { name: "roles.remove.keep" }))

		expect(revoke).not.toHaveBeenCalled()
		expect(screen.getByRole("button", { name: /roles.remove.actionFor/ })).toBeTruthy()
	})

	it("adds an assignment and clears the form", async () => {
		list.mockResolvedValue(listing())
		add.mockResolvedValue(assignment("new", { email: "new@example.com" }))
		const user = userEvent.setup()
		render(<ManageRolesPage />, { wrapper: wrap })
		await screen.findByText("a@example.com")

		await user.type(screen.getByLabelText("roles.add.email"), " new@example.com ")
		await user.selectOptions(screen.getByLabelText("roles.add.role"), "administrator")
		await user.click(screen.getByRole("button", { name: "roles.add.submit" }))

		expect(await screen.findByText("new@example.com")).toBeTruthy()
		expect(add).toHaveBeenCalledWith("new@example.com", "administrator")
		expect(screen.getByLabelText<HTMLInputElement>("roles.add.email").value).toBe("")
	})

	it("maps a refused add to its own message, not the API's prose", async () => {
		list.mockResolvedValue(listing())
		add.mockRejectedValue(new RoleAssignmentError(400, "duplicate"))
		const user = userEvent.setup()
		render(<ManageRolesPage />, { wrapper: wrap })
		await screen.findByText("a@example.com")

		await user.type(screen.getByLabelText("roles.add.email"), "a@example.com")
		await user.click(screen.getByRole("button", { name: "roles.add.submit" }))

		expect((await screen.findByRole("alert")).textContent).toBe("roles.refused.duplicate")
	})

	it("uses the generic message for a server fault on remove", async () => {
		list.mockResolvedValue(listing())
		revoke.mockRejectedValue(new RoleAssignmentError(500, "other"))
		const user = userEvent.setup()
		render(<ManageRolesPage />, { wrapper: wrap })
		await screen.findByText("a@example.com")

		await user.click(screen.getByRole("button", { name: /roles.remove.actionFor/ }))
		await user.click(screen.getByRole("button", { name: "roles.remove.yes" }))

		expect((await screen.findByRole("alert")).textContent).toBe("roles.error.remove")
		expect(screen.getByText("a@example.com")).toBeTruthy()
	})

	it("says plainly when assignments cannot be read and offers no change (REQ-AUTH-029)", async () => {
		list.mockResolvedValue({ superAdministrator: { email: "root@example.com" }, assignments: null, assignmentsUnavailable: true })
		render(<ManageRolesPage />, { wrapper: wrap })

		expect(await screen.findByText("roles.unavailable")).toBeTruthy()
		expect(screen.getByText("root@example.com")).toBeTruthy()
		expect(screen.queryByLabelText("roles.add.email")).toBeNull()
		expect(screen.queryByRole("button", { name: "roles.add.submit" })).toBeNull()
		expect(screen.queryByRole("button", { name: /roles.remove/ })).toBeNull()
		expect(screen.queryByText("roles.empty")).toBeNull()
	})

	it("shows an empty state", async () => {
		list.mockResolvedValue(listing({ superAdministrator: null, assignments: [] }))
		render(<ManageRolesPage />, { wrapper: wrap })

		expect(await screen.findByText("roles.empty")).toBeTruthy()
	})

	it("reports a failed load and retries", async () => {
		list.mockRejectedValueOnce(new Error("down")).mockResolvedValueOnce(listing())
		const user = userEvent.setup()
		render(<ManageRolesPage />, { wrapper: wrap })

		expect((await screen.findByRole("alert")).textContent).toContain("roles.error.load")
		await user.click(screen.getByRole("button", { name: "list.retry" }))

		expect(await screen.findByText("a@example.com")).toBeTruthy()
	})

	it("ignores a blank email without calling the API", async () => {
		list.mockResolvedValue(listing())
		const { result } = renderHook(() => useManageRolesPage(), { wrapper: wrap })
		await waitFor(() => expect(result.current.loading).toBe(false))

		act(() => result.current.changeEmail("   "))
		act(() => result.current.submitAdd({ preventDefault: () => {} } as never))

		expect(add).not.toHaveBeenCalled()
		expect(result.current.adding).toBe(false)
	})

	it("drops an add that finishes after the list was lost", async () => {
		list.mockResolvedValueOnce(listing())
		let finish: (value: RoleAssignment) => void = () => {}
		add.mockReturnValue(new Promise<RoleAssignment>((resolve) => (finish = resolve)))
		const { result } = renderHook(() => useManageRolesPage(), { wrapper: wrap })
		await waitFor(() => expect(result.current.loading).toBe(false))

		act(() => result.current.changeEmail("n@example.com"))
		act(() => result.current.submitAdd({ preventDefault: () => {} } as never))
		list.mockRejectedValueOnce(new Error("down"))
		await act(() => {
			result.current.retry()
			return Promise.resolve()
		})
		await act(() => {
			finish(assignment("n"))
			return Promise.resolve()
		})

		expect(result.current.assignments).toEqual([])
		expect(result.current.canChange).toBe(false)
	})

	it("drops a remove that finishes after the list was lost", async () => {
		list.mockResolvedValueOnce(listing())
		let finish: () => void = () => {}
		revoke.mockReturnValue(new Promise<void>((resolve) => (finish = resolve)))
		const { result } = renderHook(() => useManageRolesPage(), { wrapper: wrap })
		await waitFor(() => expect(result.current.loading).toBe(false))

		act(() => result.current.confirmRemove("a"))
		list.mockResolvedValueOnce({ superAdministrator: null, assignments: null, assignmentsUnavailable: true })
		await act(() => {
			result.current.retry()
			return Promise.resolve()
		})
		await act(() => {
			finish()
			return Promise.resolve()
		})

		expect(result.current.assignments).toEqual([])
		expect(result.current.unavailable).toBe(true)
	})
})
