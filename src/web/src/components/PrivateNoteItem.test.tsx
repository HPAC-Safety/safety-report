import { act, render, renderHook, screen } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"

const history = vi.hoisted(() => vi.fn())
vi.mock("../api/adminReports", () => ({ privateNoteHistory: history }))
vi.mock("./PrivateNoteItem.view", () => ({
	PrivateNoteItemView: (props: { note: { id: string } }) => <p data-testid="view">{props.note.id}</p>,
}))

import { PrivateNoteItem, usePrivateNoteItem, type PrivateNote, type PrivateNoteItemProps } from "./PrivateNoteItem"

const note = (over: Partial<PrivateNote> = {}): PrivateNote => ({
	id: "n1",
	text: "text",
	revision: 1,
	writtenBy: "sub",
	writtenAt: "2026-01-02T03:04:05Z",
	createdAt: "2026-01-02T03:04:05Z",
	edited: false,
	isMine: true,
	attachment: null,
	...over,
})
const base = (over: Partial<PrivateNoteItemProps> = {}): PrivateNoteItemProps => ({
	reportId: "r1",
	note: note(),
	attachments: [],
	format: (value) => value,
	onEdit: vi.fn().mockResolvedValue(true),
	onRemove: vi.fn().mockResolvedValue(true),
	...over,
})
const revisions = [{ number: 1, text: "old", writtenBy: "s", writtenAt: "x", isMine: true, attachment: null }]

describe("usePrivateNoteItem", () => {
	beforeEach(() => history.mockReset())

	it("names the attachment an edit starts with, unless it is missing or removed", () => {
		const attached = renderHook(() => usePrivateNoteItem(base({ note: note({ attachment: { id: "a1", fileName: "f", removed: false } }) })))
		const removed = renderHook(() => usePrivateNoteItem(base({ note: note({ attachment: { id: "a1", fileName: "f", removed: true } }) })))
		const none = renderHook(() => usePrivateNoteItem(base()))

		expect(attached.result.current.editAttachment).toBe("a1")
		expect(removed.result.current.editAttachment).toBeNull()
		expect(none.result.current.editAttachment).toBeNull()
	})

	it("switches between reading and editing", () => {
		const { result } = renderHook(() => usePrivateNoteItem(base()))

		act(() => result.current.startEditing())
		expect(result.current.editing).toBe(true)
		act(() => result.current.stopEditing())
		expect(result.current.editing).toBe(false)
	})

	it("asks before removing and can keep the note", () => {
		const { result } = renderHook(() => usePrivateNoteItem(base()))

		act(() => result.current.askRemove())
		expect(result.current.confirming).toBe(true)
		act(() => result.current.keep())
		expect(result.current.confirming).toBe(false)
	})

	it("removes the note, then stops asking", async () => {
		const props = base()
		const { result } = renderHook(() => usePrivateNoteItem(props))
		act(() => result.current.askRemove())

		await act(async () => result.current.confirmRemove())

		expect(props.onRemove).toHaveBeenCalled()
		expect(result.current.confirming).toBe(false)
	})

	it("loads the history, then hides it", async () => {
		history.mockResolvedValue(revisions)
		const { result } = renderHook(() => usePrivateNoteItem(base()))

		await act(async () => result.current.toggleHistory())
		expect(history).toHaveBeenCalledWith("r1", "n1")
		expect(result.current.history).toEqual(revisions)

		await act(async () => result.current.toggleHistory())
		expect(result.current.history).toBeNull()
	})

	it("says when the history could not load, and tries again", async () => {
		history.mockRejectedValueOnce(new Error("no")).mockResolvedValueOnce(revisions)
		const { result } = renderHook(() => usePrivateNoteItem(base()))

		await act(async () => result.current.toggleHistory())
		expect(result.current.historyFailed).toBe(true)

		await act(async () => result.current.toggleHistory())
		expect(result.current.historyFailed).toBe(false)
		expect(result.current.history).toEqual(revisions)
	})

	it("leaves the editor and drops a stale history after a saved edit", async () => {
		history.mockResolvedValue(revisions)
		const props = base()
		const { result } = renderHook(() => usePrivateNoteItem(props))
		await act(async () => result.current.toggleHistory())
		act(() => result.current.startEditing())

		let saved = false
		await act(async () => {
			saved = await result.current.saveEdit("new", "a1")
		})

		expect(saved).toBe(true)
		expect(props.onEdit).toHaveBeenCalledWith("new", "a1")
		expect(result.current.editing).toBe(false)
		expect(result.current.history).toBeNull()
	})

	it("stays in the editor when the edit was refused", async () => {
		history.mockResolvedValue(revisions)
		const props = base({ onEdit: vi.fn().mockResolvedValue(false) })
		const { result } = renderHook(() => usePrivateNoteItem(props))
		await act(async () => result.current.toggleHistory())
		act(() => result.current.startEditing())

		let saved = true
		await act(async () => {
			saved = await result.current.saveEdit("new", null)
		})

		expect(saved).toBe(false)
		expect(result.current.editing).toBe(true)
		expect(result.current.history).toEqual(revisions)
	})
})

describe("PrivateNoteItem", () => {
	it("renders its view with the view model", () => {
		render(<PrivateNoteItem {...base()} />)

		expect(screen.getByTestId("view").textContent).toBe("n1")
	})
})
