import { act, render, renderHook, screen } from "@testing-library/react"
import type { FormEvent } from "react"
import { beforeEach, describe, expect, it, vi } from "vitest"

const guard = vi.hoisted(() => vi.fn())
vi.mock("../hooks/useUnsavedChangesGuard", () => ({ useUnsavedChangesGuard: guard }))
vi.mock("./PrivateNoteComposer.view", () => ({
	PrivateNoteComposerView: (props: { text: string }) => <p data-testid="view">{props.text}</p>,
}))

import { PRIVATE_NOTE_MAX_LENGTH } from "../api/adminReports"
import { PrivateNoteComposer, usePrivateNoteComposer, type PrivateNoteComposerProps } from "./PrivateNoteComposer"

const event = () => ({ preventDefault: vi.fn() }) as unknown as FormEvent & { preventDefault: ReturnType<typeof vi.fn> }
const base = (over: Partial<PrivateNoteComposerProps> = {}): PrivateNoteComposerProps => ({
	attachments: [],
	onSave: vi.fn().mockResolvedValue(true),
	...over,
})

describe("usePrivateNoteComposer", () => {
	beforeEach(() => guard.mockClear())

	it("starts empty and not dirty", () => {
		const { result } = renderHook(() => usePrivateNoteComposer(base()))

		expect(result.current).toMatchObject({ text: "", attachmentId: null, length: 0, submitDisabled: true, maxLength: PRIVATE_NOTE_MAX_LENGTH })
		expect(guard).toHaveBeenLastCalledWith(false)
	})

	it("starts from the note being edited and guards unsaved changes", () => {
		const { result } = renderHook(() => usePrivateNoteComposer(base({ initial: "old", initialAttachment: "a1" })))

		expect(result.current.text).toBe("old")
		expect(result.current.attachmentId).toBe("a1")
		expect(guard).toHaveBeenLastCalledWith(false)

		act(() => result.current.changeText("new"))
		expect(guard).toHaveBeenLastCalledWith(true)

		act(() => result.current.changeText("old"))
		act(() => result.current.changeAttachment("a2"))
		expect(guard).toHaveBeenLastCalledWith(true)
	})

	it("treats an empty choice as no attachment", () => {
		const { result } = renderHook(() => usePrivateNoteComposer(base({ initialAttachment: "a1" })))

		act(() => result.current.changeAttachment(""))

		expect(result.current.attachmentId).toBeNull()
	})

	it("refuses to save a blank note", async () => {
		const props = base()
		const { result } = renderHook(() => usePrivateNoteComposer(props))
		const submitted = event()

		await act(async () => result.current.submit(submitted))

		expect(submitted.preventDefault).toHaveBeenCalled()
		expect(props.onSave).not.toHaveBeenCalled()
	})

	it("refuses to save a note that is too long", async () => {
		const props = base()
		const { result } = renderHook(() => usePrivateNoteComposer(props))

		act(() => result.current.changeText("x".repeat(PRIVATE_NOTE_MAX_LENGTH + 1)))
		expect(result.current.tooLong).toBe(true)
		expect(result.current.submitDisabled).toBe(true)
		await act(async () => result.current.submit(event()))

		expect(props.onSave).not.toHaveBeenCalled()
	})

	it("saves, disables the button meanwhile, and clears a new note", async () => {
		let finish: (saved: boolean) => void = () => {}
		const props = base({ onSave: vi.fn(() => new Promise<boolean>((resolve) => (finish = resolve))) })
		const { result } = renderHook(() => usePrivateNoteComposer(props))
		act(() => result.current.changeText("a note"))
		act(() => result.current.changeAttachment("a1"))

		act(() => result.current.submit(event()))
		expect(props.onSave).toHaveBeenCalledWith("a note", "a1")
		expect(result.current.submitDisabled).toBe(true)

		await act(async () => finish(true))
		expect(result.current).toMatchObject({ text: "", attachmentId: null, submitDisabled: true })
	})

	it("keeps the text when the save was refused", async () => {
		const props = base({ onSave: vi.fn().mockResolvedValue(false) })
		const { result } = renderHook(() => usePrivateNoteComposer(props))
		act(() => result.current.changeText("a note"))

		await act(async () => result.current.submit(event()))

		expect(result.current.text).toBe("a note")
		expect(result.current.submitDisabled).toBe(false)
	})

	it("keeps the text of an edit after it saves", async () => {
		const props = base({ initial: "old", onCancel: vi.fn() })
		const { result } = renderHook(() => usePrivateNoteComposer(props))
		act(() => result.current.changeText("edited"))

		await act(async () => result.current.submit(event()))

		expect(props.onSave).toHaveBeenCalledWith("edited", null)
		expect(result.current.text).toBe("edited")
	})
})

describe("PrivateNoteComposer", () => {
	it("renders its view with the view model", () => {
		render(<PrivateNoteComposer {...base({ initial: "hello" })} />)

		expect(screen.getByTestId("view").textContent).toBe("hello")
	})
})
