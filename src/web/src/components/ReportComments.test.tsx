import { act, render, renderHook, screen, waitFor } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"

const api = vi.hoisted(() => ({
	listComments: vi.fn(),
	postComment: vi.fn(),
	editComment: vi.fn(),
	deleteComment: vi.fn(),
	hideComment: vi.fn(),
}))
const auth = vi.hoisted((): { value: { isSignedIn: boolean; role: string | null } } => ({ value: { isSignedIn: true, role: "user" } }))
const t = vi.hoisted(() => (key: string) => key)
vi.mock("../i18n/useLocale", () => ({ useLocale: () => ({ locale: "fr-CA", t }) }))
vi.mock("../auth/useAuth", () => ({ useAuth: () => auth.value }))
vi.mock("../api/publicReports", () => api)
vi.mock("./ReportComments.view", () => ({
	ReportCommentsView: (props: { reportId: string; comments: unknown[] | null }) => (
		<p data-testid="view">{props.reportId}:{props.comments?.length ?? "none"}</p>
	),
}))

import { ReportComments, useReportComments, type PublicComment } from "./ReportComments"

const comment = { id: "c1" } as PublicComment

describe("useReportComments", () => {
	beforeEach(() => {
		for (const fn of Object.values(api)) fn.mockReset()
		api.listComments.mockResolvedValue([comment])
		for (const fn of [api.postComment, api.editComment, api.deleteComment, api.hideComment]) fn.mockResolvedValue(undefined)
		auth.value = { isSignedIn: true, role: "user" }
	})

	it("loads the comments of the report", async () => {
		const { result } = renderHook(() => useReportComments({ reportId: "r1" }))

		expect(result.current.comments).toBeNull()
		await waitFor(() => expect(result.current.comments).toEqual([comment]))
		expect(api.listComments).toHaveBeenCalledWith("r1")
		expect(result.current).toMatchObject({ failed: false, error: null, isSignedIn: true })
	})

	it("says when the comments could not load", async () => {
		api.listComments.mockRejectedValue(new Error("no"))
		const { result } = renderHook(() => useReportComments({ reportId: "r1" }))

		await waitFor(() => expect(result.current.failed).toBe(true))
	})

	it.each([
		["safety_officer", true],
		["administrator", true],
		["user", false],
		[null, false],
	])("offers the hide control to %s: %s", async (role, expected) => {
		auth.value = { isSignedIn: role !== null, role }
		const { result } = renderHook(() => useReportComments({ reportId: "r1" }))
		await waitFor(() => expect(result.current.comments).not.toBeNull())

		expect(result.current.canHide).toBe(expected)
	})

	it("posts, edits, deletes, and hides, reloading after each", async () => {
		const { result } = renderHook(() => useReportComments({ reportId: "r1" }))
		await waitFor(() => expect(result.current.comments).not.toBeNull())

		await act(async () => {
			expect(await result.current.post("hello")).toBe(true)
			expect(await result.current.edit(comment, "edited")).toBe(true)
			expect(await result.current.remove(comment)).toBe(true)
			expect(await result.current.hide(comment)).toBe(true)
		})

		expect(api.postComment).toHaveBeenCalledWith("r1", "hello", "fr-CA")
		expect(api.editComment).toHaveBeenCalledWith("r1", "c1", "edited", "fr-CA")
		expect(api.deleteComment).toHaveBeenCalledWith("r1", "c1")
		expect(api.hideComment).toHaveBeenCalledWith("c1")
		expect(api.listComments).toHaveBeenCalledTimes(5)
	})

	it("says a refused change could not be saved, and clears the message on the next try", async () => {
		api.postComment.mockRejectedValueOnce(new Error("no"))
		const { result } = renderHook(() => useReportComments({ reportId: "r1" }))
		await waitFor(() => expect(result.current.comments).not.toBeNull())

		await act(async () => {
			expect(await result.current.post("hello")).toBe(false)
		})
		expect(result.current.error).toBe("comments.error.save")

		await act(async () => await result.current.post("hello"))
		expect(result.current.error).toBeNull()
	})
})

describe("ReportComments", () => {
	it("renders its view with the view model", async () => {
		api.listComments.mockResolvedValue([comment])
		render(<ReportComments reportId="r1" />)

		await waitFor(() => expect(screen.getByTestId("view").textContent).toBe("r1:1"))
	})
})
