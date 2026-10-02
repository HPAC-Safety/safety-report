import { act, render, renderHook, screen, waitFor } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"

const api = vi.hoisted(() => ({
	listPrivateNotes: vi.fn(),
	addPrivateNote: vi.fn(),
	editPrivateNote: vi.fn(),
	removePrivateNote: vi.fn(),
}))
const t = vi.hoisted(() => (key: string) => key)
vi.mock("../i18n/useLocale", () => ({ useLocale: () => ({ locale: "en-CA", t }) }))
vi.mock("../api/adminReports", () => ({ ...api, STALE_PRIVATE_NOTE: "stale-note" }))
vi.mock("../api/adminQuestions", () => ({
	ApiError: class ApiError extends Error {
		constructor(
			public status: number,
			public type: string,
		) {
			super("api")
		}
	},
}))
vi.mock("./PrivateNotes.view", () => ({
	PrivateNotesView: (props: { reportId: string; notes: unknown[] | null }) => (
		<p data-testid="view">{props.reportId}:{props.notes?.length ?? "none"}</p>
	),
}))

import { ApiError } from "../api/adminQuestions"
import { PrivateNotes, usePrivateNotes, type PrivateNote } from "./PrivateNotes"

const note = (id: string) => ({ id, text: id, writtenAt: "2026-01-02T03:04:05Z" }) as PrivateNote
const apiError = (type: string) => new (ApiError as unknown as new (status: number, type: string) => Error)(409, type)

describe("usePrivateNotes", () => {
	beforeEach(() => {
		for (const fn of Object.values(api)) fn.mockReset()
		api.listPrivateNotes.mockResolvedValue([note("n1")])
		api.addPrivateNote.mockResolvedValue(undefined)
		api.editPrivateNote.mockResolvedValue(undefined)
		api.removePrivateNote.mockResolvedValue(undefined)
	})

	it("loads the notes of the report", async () => {
		const { result } = renderHook(() => usePrivateNotes({ reportId: "r1" }))

		expect(result.current.notes).toBeNull()
		await waitFor(() => expect(result.current.notes).toEqual([note("n1")]))
		expect(api.listPrivateNotes).toHaveBeenCalledWith("r1")
		expect(result.current.failed).toBe(false)
		expect(result.current.attachments).toEqual([])
	})

	it("passes the report's private attachments through", () => {
		const attachments = [{ id: "a1" }] as never
		const { result } = renderHook(() => usePrivateNotes({ reportId: "r1", attachments }))

		expect(result.current.attachments).toBe(attachments)
	})

	it("treats anything but a list as a failed read", async () => {
		api.listPrivateNotes.mockResolvedValue({})
		const { result } = renderHook(() => usePrivateNotes({ reportId: "r1" }))

		await waitFor(() => expect(result.current.failed).toBe(true))
		expect(result.current.notes).toBeNull()
	})

	it("treats a refused read as a failed read", async () => {
		api.listPrivateNotes.mockRejectedValue(new Error("no"))
		const { result } = renderHook(() => usePrivateNotes({ reportId: "r1" }))

		await waitFor(() => expect(result.current.failed).toBe(true))
	})

	it("formats a date in the reader's locale", async () => {
		const { result } = renderHook(() => usePrivateNotes({ reportId: "r1" }))
		await waitFor(() => expect(result.current.notes).not.toBeNull())

		expect(result.current.format("2026-01-02T03:04:05Z")).toContain("2026")
	})

	it("adds, edits, and removes a note, then reloads", async () => {
		const { result } = renderHook(() => usePrivateNotes({ reportId: "r1" }))
		await waitFor(() => expect(result.current.notes).not.toBeNull())
		const existing = note("n1")

		await act(async () => {
			expect(await result.current.add("text", "a1")).toBe(true)
			expect(await result.current.edit(existing, "edited", null)).toBe(true)
			expect(await result.current.remove(existing)).toBe(true)
		})

		expect(api.addPrivateNote).toHaveBeenCalledWith("r1", "text", "a1")
		expect(api.editPrivateNote).toHaveBeenCalledWith("r1", existing, "edited", null)
		expect(api.removePrivateNote).toHaveBeenCalledWith("r1", "n1")
		expect(api.listPrivateNotes).toHaveBeenCalledTimes(4)
		expect(result.current.error).toBeNull()
	})

	it("says another reviewer changed the note first", async () => {
		api.addPrivateNote.mockRejectedValue(apiError("stale-note"))
		const { result } = renderHook(() => usePrivateNotes({ reportId: "r1" }))
		await waitFor(() => expect(result.current.notes).not.toBeNull())

		await act(async () => {
			expect(await result.current.add("text", null)).toBe(false)
		})

		expect(result.current.error).toBe("privateNotes.error.stale")
		expect(api.listPrivateNotes).toHaveBeenCalledTimes(2)
	})

	it("says any other failure could not be saved, and clears the message on the next try", async () => {
		api.addPrivateNote.mockRejectedValueOnce(apiError("other")).mockRejectedValueOnce(new Error("no")).mockResolvedValueOnce(undefined)
		const { result } = renderHook(() => usePrivateNotes({ reportId: "r1" }))
		await waitFor(() => expect(result.current.notes).not.toBeNull())

		await act(async () => void (await result.current.add("text", null)))
		expect(result.current.error).toBe("privateNotes.error.save")
		await act(async () => void (await result.current.add("text", null)))
		expect(result.current.error).toBe("privateNotes.error.save")
		await act(async () => void (await result.current.add("text", null)))
		expect(result.current.error).toBeNull()
	})
})

describe("PrivateNotes", () => {
	it("renders its view with the view model", async () => {
		api.listPrivateNotes.mockResolvedValue([note("n1")])
		render(<PrivateNotes reportId="r1" />)

		await waitFor(() => expect(screen.getByTestId("view").textContent).toBe("r1:1"))
	})
})
