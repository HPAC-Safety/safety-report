import { act, render, renderHook, screen } from "@testing-library/react"
import { describe, expect, it, vi } from "vitest"

vi.mock("./CommentItem.view", () => ({
	CommentItemView: (props: { shownText: string }) => <p data-testid="view">{props.shownText}</p>,
}))

import { CommentItem, useCommentItem, type CommentItemProps, type PublicComment } from "./CommentItem"

const comment = (over: Partial<PublicComment> = {}): PublicComment => ({
	id: "c1",
	text: "written",
	locale: "en-CA",
	translatedText: null,
	createdAt: "2026-01-02T03:04:05Z",
	updatedAt: "2026-01-02T03:04:05Z",
	edited: false,
	isMine: true,
	...over,
})
const base = (over: Partial<CommentItemProps> = {}): CommentItemProps => ({
	comment: comment(),
	locale: "en-CA",
	canHide: false,
	onEdit: vi.fn().mockResolvedValue(true),
	onDelete: vi.fn().mockResolvedValue(true),
	onHide: vi.fn().mockResolvedValue(true),
	...over,
})

describe("useCommentItem", () => {
	it("shows a comment written in the reader's language as written", () => {
		const { result } = renderHook(() => useCommentItem(base()))

		expect(result.current).toMatchObject({ written: true, translated: false, shownText: "written", shownLocale: "en-CA" })
		expect(result.current.postedAt).toContain("2026")
	})

	it("shows the machine translation in the reader's language", () => {
		const { result } = renderHook(() =>
			useCommentItem(base({ comment: comment({ locale: "fr-CA", text: "écrit", translatedText: "translated" }) })),
		)

		expect(result.current).toMatchObject({ written: false, translated: true, shownText: "translated", shownLocale: "en-CA" })
	})

	it("shows the original while it awaits translation", () => {
		const { result } = renderHook(() => useCommentItem(base({ comment: comment({ locale: "fr-CA", text: "écrit" }) })))

		expect(result.current).toMatchObject({ written: false, translated: false, shownText: "écrit", shownLocale: "fr-CA" })
	})

	it("switches between reading and editing", () => {
		const { result } = renderHook(() => useCommentItem(base()))

		act(() => result.current.startEditing())
		expect(result.current.editing).toBe(true)
		act(() => result.current.stopEditing())
		expect(result.current.editing).toBe(false)
	})

	it("leaves the editor after a saved edit only", async () => {
		const props = base({ onEdit: vi.fn().mockResolvedValueOnce(false).mockResolvedValueOnce(true) })
		const { result } = renderHook(() => useCommentItem(props))
		act(() => result.current.startEditing())

		await act(async () => await result.current.saveEdit("one"))
		expect(result.current.editing).toBe(true)
		await act(async () => await result.current.saveEdit("two"))

		expect(props.onEdit).toHaveBeenLastCalledWith("two")
		expect(result.current.editing).toBe(false)
	})

	it("asks before deleting, then deletes", async () => {
		const props = base()
		const { result } = renderHook(() => useCommentItem(props))

		act(() => result.current.askDelete())
		expect(result.current.confirming).toBe("delete")
		await act(async () => result.current.confirmed())

		expect(props.onDelete).toHaveBeenCalled()
		expect(props.onHide).not.toHaveBeenCalled()
		expect(result.current.confirming).toBeNull()
	})

	it("asks before hiding, then hides", async () => {
		const props = base({ canHide: true })
		const { result } = renderHook(() => useCommentItem(props))

		act(() => result.current.askHide())
		expect(result.current.confirming).toBe("hide")
		await act(async () => result.current.confirmed())

		expect(props.onHide).toHaveBeenCalled()
		expect(props.onDelete).not.toHaveBeenCalled()
	})

	it("can keep the comment", () => {
		const { result } = renderHook(() => useCommentItem(base()))

		act(() => result.current.askDelete())
		act(() => result.current.keep())

		expect(result.current.confirming).toBeNull()
	})
})

describe("CommentItem", () => {
	it("renders its view with the view model", () => {
		render(<CommentItem {...base()} />)

		expect(screen.getByTestId("view").textContent).toBe("written")
	})
})
