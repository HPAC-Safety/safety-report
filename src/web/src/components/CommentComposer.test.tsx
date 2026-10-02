import { act, render, renderHook, screen } from "@testing-library/react"
import type { FormEvent } from "react"
import { beforeEach, describe, expect, it, vi } from "vitest"

const guard = vi.hoisted(() => vi.fn())
vi.mock("../hooks/useUnsavedChangesGuard", () => ({ useUnsavedChangesGuard: guard }))
vi.mock("./CommentComposer.view", () => ({
	CommentComposerView: (props: { text: string }) => <p data-testid="view">{props.text}</p>,
}))

import { COMMENT_MAX_LENGTH } from "../api/publicReports"
import { CommentComposer, useCommentComposer, type CommentComposerProps } from "./CommentComposer"

const event = () => ({ preventDefault: vi.fn() }) as unknown as FormEvent & { preventDefault: ReturnType<typeof vi.fn> }
const base = (over: Partial<CommentComposerProps> = {}): CommentComposerProps => ({
	onPost: vi.fn().mockResolvedValue(true),
	...over,
})

describe("useCommentComposer", () => {
	beforeEach(() => guard.mockClear())

	it("starts empty and not dirty", () => {
		const { result } = renderHook(() => useCommentComposer(base()))

		expect(result.current).toMatchObject({ text: "", length: 0, submitDisabled: true, maxLength: COMMENT_MAX_LENGTH })
		expect(guard).toHaveBeenLastCalledWith(false)
	})

	it("guards unsaved changes to the comment being edited", () => {
		const { result } = renderHook(() => useCommentComposer(base({ initial: "old" })))

		expect(result.current.text).toBe("old")
		act(() => result.current.changeText("new"))

		expect(guard).toHaveBeenLastCalledWith(true)
	})

	it("refuses to post a blank comment", async () => {
		const props = base()
		const { result } = renderHook(() => useCommentComposer(props))
		const submitted = event()

		await act(async () => result.current.submit(submitted))

		expect(submitted.preventDefault).toHaveBeenCalled()
		expect(props.onPost).not.toHaveBeenCalled()
	})

	it("refuses to post a comment that is too long", async () => {
		const props = base()
		const { result } = renderHook(() => useCommentComposer(props))

		act(() => result.current.changeText("x".repeat(COMMENT_MAX_LENGTH + 1)))
		expect(result.current.tooLong).toBe(true)
		await act(async () => result.current.submit(event()))

		expect(props.onPost).not.toHaveBeenCalled()
	})

	it("posts, disables the button meanwhile, and clears a new comment", async () => {
		let finish: (saved: boolean) => void = () => {}
		const props = base({ onPost: vi.fn(() => new Promise<boolean>((resolve) => (finish = resolve))) })
		const { result } = renderHook(() => useCommentComposer(props))
		act(() => result.current.changeText("hello"))

		act(() => result.current.submit(event()))
		expect(props.onPost).toHaveBeenCalledWith("hello")
		expect(result.current.submitDisabled).toBe(true)

		await act(async () => finish(true))
		expect(result.current.text).toBe("")
	})

	it("keeps the text when the post was refused", async () => {
		const props = base({ onPost: vi.fn().mockResolvedValue(false) })
		const { result } = renderHook(() => useCommentComposer(props))
		act(() => result.current.changeText("hello"))

		await act(async () => result.current.submit(event()))

		expect(result.current.text).toBe("hello")
	})

	it("keeps the text of an edit after it saves", async () => {
		const props = base({ initial: "old", onCancel: vi.fn() })
		const { result } = renderHook(() => useCommentComposer(props))
		act(() => result.current.changeText("edited"))

		await act(async () => result.current.submit(event()))

		expect(result.current.text).toBe("edited")
	})
})

describe("CommentComposer", () => {
	it("renders its view with the view model", () => {
		render(<CommentComposer {...base({ initial: "hello" })} />)

		expect(screen.getByTestId("view").textContent).toBe("hello")
	})
})
