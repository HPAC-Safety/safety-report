import type { RefObject } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { cleanup, fireEvent, render, renderHook, screen } from "@testing-library/react"
import type { DiffPart } from "../lib/wordDiff"
import { TranslateConfirmDialog, useTranslateConfirmDialog } from "./TranslateConfirmDialog"

let startOpen = false

vi.mock("./TranslateConfirmDialog.view", () => ({
	TranslateConfirmDialogView: (props: {
		dialogRef: RefObject<HTMLDialogElement>
		keepButtonRef: RefObject<HTMLButtonElement>
		onKeep: () => void
		currentParts: DiffPart[]
		proposedParts: DiffPart[]
	}) => (
		<dialog ref={props.dialogRef} open={startOpen}>
			<button ref={props.keepButtonRef} type="button" onClick={props.onKeep}>
				keep
			</button>
			<p data-testid="current">{props.currentParts.map((part) => `${part.kind}:${part.text}`).join("|")}</p>
			<p data-testid="proposed">{props.proposedParts.map((part) => `${part.kind}:${part.text}`).join("|")}</p>
		</dialog>
	),
}))

const showModal = vi.fn(function (this: HTMLDialogElement) {
	this.setAttribute("open", "")
})

beforeEach(() => {
	startOpen = false
	showModal.mockClear()
	HTMLDialogElement.prototype.showModal = showModal
	HTMLDialogElement.prototype.close = vi.fn()
})

afterEach(() => {
	cleanup()
	vi.restoreAllMocks()
})

const props = { target: "en" as const, current: "red car", proposed: "blue car", onAccept: () => {}, onKeep: () => {} }

describe("TranslateConfirmDialog", () => {
	it("opens the dialog as a modal and focuses its safe choice when it mounts", () => {
		render(<TranslateConfirmDialog {...props} />)

		expect(showModal).toHaveBeenCalledTimes(1)
		expect(document.activeElement).toBe(screen.getByRole("button", { hidden: true }))
	})

	it("does not open the dialog again when it is already open", () => {
		startOpen = true

		render(<TranslateConfirmDialog {...props} />)

		expect(showModal).not.toHaveBeenCalled()
	})

	it("hands its own props on to the view", () => {
		const onKeep = vi.fn()

		render(<TranslateConfirmDialog {...props} onKeep={onKeep} />)
		fireEvent.click(screen.getByRole("button"))

		expect(onKeep).toHaveBeenCalledTimes(1)
	})

	it("shows the current side without additions and the proposed side without removals", () => {
		render(<TranslateConfirmDialog {...props} />)

		expect(screen.getByTestId("current").textContent).toBe("removed:red |same:car")
		expect(screen.getByTestId("proposed").textContent).toBe("added:blue |same:car")
	})
})

describe("useTranslateConfirmDialog", () => {
	it("returns the refs, empty until rendered, and the two sides of the difference", () => {
		const { result } = renderHook(() => useTranslateConfirmDialog(props))

		expect(result.current.dialogRef.current).toBeNull()
		expect(result.current.keepButtonRef.current).toBeNull()
		expect(result.current.currentParts.map((part) => part.kind)).toEqual(["removed", "same"])
		expect(result.current.proposedParts.map((part) => part.kind)).toEqual(["added", "same"])
	})
})
