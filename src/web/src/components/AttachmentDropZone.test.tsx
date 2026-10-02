import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { AttachmentDropZone, carriesFiles } from "./AttachmentDropZone"

afterEach(cleanup)

const files = (types: string[], list: File[] = []) => ({ types, files: list, dropEffect: "none" })

function zone(onFiles = vi.fn(), over: Partial<Parameters<typeof AttachmentDropZone>[0]> = {}) {
	const { container } = render(
		<AttachmentDropZone
			fieldId="f"
			describedBy="hint"
			guidanceId="g"
			guidance="Up to 25 MB"
			promptText="Drop here"
			onFiles={onFiles}
			{...over}
		/>,
	)
	return container.firstElementChild as HTMLElement
}

describe("carriesFiles", () => {
	it("is true only for a drag that carries files", () => {
		expect(carriesFiles({ dataTransfer: { types: ["Files"] } as unknown as DataTransfer })).toBe(true)
		expect(carriesFiles({ dataTransfer: { types: ["text/plain"] } as unknown as DataTransfer })).toBe(false)
		expect(carriesFiles({ dataTransfer: null })).toBe(false)
	})
})

describe("AttachmentDropZone", () => {
	it("describes its button by the guidance and the caller's hint", () => {
		zone()
		expect(screen.getByRole("button", { name: "Drop here" }).getAttribute("aria-describedby")).toBe("g hint")
	})

	it("describes its button by the guidance alone when the caller has no hint", () => {
		zone(vi.fn(), { describedBy: undefined })
		expect(screen.getByRole("button", { name: "Drop here" }).getAttribute("aria-describedby")).toBe("g")
	})

	it("highlights while files are dragged over and steadily across children", () => {
		const element = zone()
		fireEvent.dragEnter(element, { dataTransfer: files(["Files"]) })
		fireEvent.dragEnter(element, { dataTransfer: files(["Files"]) })
		expect(element.className).toContain("bg-surface-3")
		fireEvent.dragLeave(element, { dataTransfer: files(["Files"]) })
		expect(element.className).toContain("bg-surface-3")
		fireEvent.dragLeave(element, { dataTransfer: files(["Files"]) })
		expect(element.className).toContain("border-rule")
		fireEvent.dragLeave(element, { dataTransfer: files(["Files"]) })
		expect(element.className).not.toContain("bg-surface-3")
	})

	it("ignores a drag that carries no files", () => {
		const element = zone()
		fireEvent.dragEnter(element, { dataTransfer: files(["text/plain"]) })
		fireEvent.dragOver(element, { dataTransfer: files(["text/plain"]) })
		fireEvent.dragLeave(element, { dataTransfer: files(["text/plain"]) })
		fireEvent.drop(element, { dataTransfer: files(["text/plain"]) })
		expect(element.className).not.toContain("bg-surface-3")
	})

	it("marks a file drag as a copy", () => {
		const element = zone()
		const dataTransfer = files(["Files"])
		fireEvent.dragOver(element, { dataTransfer })
		expect(dataTransfer.dropEffect).toBe("copy")
	})

	it("hands dropped files to the caller and ends the highlight", () => {
		const onFiles = vi.fn()
		const element = zone(onFiles)
		const file = new File(["x"], "a.png")
		fireEvent.dragEnter(element, { dataTransfer: files(["Files"]) })
		fireEvent.drop(element, { dataTransfer: files(["Files"], [file]) })
		expect(onFiles).toHaveBeenCalledWith([file])
		expect(element.className).not.toContain("bg-surface-3")
	})

	it("opens the file chooser from the button", () => {
		zone()
		const input = document.getElementById("f") as HTMLInputElement
		const click = vi.spyOn(input, "click")
		fireEvent.click(screen.getByRole("button", { name: "Drop here" }))
		expect(click).toHaveBeenCalled()
	})

	it("hands chosen files to the caller and clears the input so the same file can be chosen again", () => {
		const onFiles = vi.fn()
		zone(onFiles, { accept: "image/*", multiple: false })
		const input = document.getElementById("f") as HTMLInputElement
		const file = new File(["x"], "a.png")
		fireEvent.change(input, { target: { files: [file] } })
		expect(onFiles).toHaveBeenCalledWith([file])
		expect(input.value).toBe("")
		expect(input.multiple).toBe(false)
		expect(input.accept).toBe("image/*")
	})

	it("hands over nothing when the chooser reports no files", () => {
		const onFiles = vi.fn()
		zone(onFiles)
		const input = document.getElementById("f") as HTMLInputElement
		Object.defineProperty(input, "files", { value: null, configurable: true })
		fireEvent.change(input)
		expect(onFiles).toHaveBeenCalledWith([])
	})

	it("takes a custom test id", () => {
		zone(vi.fn(), { testId: "mine" })
		expect(screen.getByTestId("mine")).toBeTruthy()
	})
})
