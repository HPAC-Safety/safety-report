import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { MultiSelectPicker, type MultiSelectPickerProps } from "./MultiSelectPicker"

afterEach(cleanup)

const groups = [[{ key: "a", label: "Alpha" }], [{ key: "b", label: "Beta" }, { key: "c", label: "Gamma" }]]

function renderPicker(overrides: Partial<MultiSelectPickerProps> = {}) {
	const onToggle = vi.fn()
	const view = render(
		<div>
			<MultiSelectPicker
				fieldId="q"
				label="Pick"
				groups={groups}
				values={[]}
				placeholder="Choose some"
				describedBy="d"
				onToggle={onToggle}
				{...overrides}
			/>
			<button id="outside">outside</button>
		</div>,
	)
	return { ...view, onToggle }
}

const trigger = () => document.getElementById("q") as HTMLButtonElement
const panel = () => document.getElementById("q-options")

describe("MultiSelectPicker", () => {
	it("shows the placeholder while nothing is chosen, and the chosen labels after", () => {
		const { unmount } = renderPicker()
		expect(document.getElementById("q-summary")?.textContent).toBe("Choose some")
		expect(trigger().getAttribute("aria-invalid")).toBeNull()
		unmount()

		renderPicker({ values: ["c", "a", "zzz"], invalid: true })
		expect(document.getElementById("q-summary")?.textContent).toBe("Alpha, Gamma")
		expect(trigger().getAttribute("aria-invalid")).toBe("true")
	})

	it("is a combobox whose popup is a dialog of checkboxes labelled by the question", () => {
		renderPicker()

		expect(trigger().getAttribute("role")).toBe("combobox")
		expect(trigger().getAttribute("aria-haspopup")).toBe("dialog")
		expect(trigger().getAttribute("aria-expanded")).toBe("false")
		fireEvent.click(trigger())
		expect(trigger().getAttribute("aria-expanded")).toBe("true")
		expect(trigger().getAttribute("aria-controls")).toBe("q-options")
		expect(panel()?.getAttribute("role")).toBe("dialog")
		expect(panel()?.getAttribute("aria-labelledby")).toBe("q-label")
	})

	it("opens and closes on the trigger, with a separator between groups", () => {
		const { container } = renderPicker()

		expect(panel()).toBeNull()
		fireEvent.click(trigger())
		expect(panel()).not.toBeNull()
		expect(container.querySelectorAll("[data-separator]")).toHaveLength(1)
		expect(screen.getAllByRole("checkbox")).toHaveLength(3)
		fireEvent.click(trigger())
		expect(panel()).toBeNull()
	})

	it("toggles an option, and shows chosen options ticked", () => {
		const { onToggle } = renderPicker({ values: ["b"] })

		fireEvent.click(trigger())
		const [alpha, beta] = screen.getAllByRole<HTMLInputElement>("checkbox")
		expect(beta.checked).toBe(true)
		fireEvent.click(alpha)

		expect(onToggle).toHaveBeenCalledWith("a")
	})

	it("keeps a locked option ticked but not toggleable, and says why", () => {
		renderPicker({ values: ["a"], locked: ["a"], lockedReason: "Needed by a child" })

		fireEvent.click(trigger())
		const [alpha, beta] = screen.getAllByRole<HTMLInputElement>("checkbox")

		expect(alpha.disabled).toBe(true)
		expect(alpha.getAttribute("aria-describedby")).toBe("q-locked-reason")
		expect(beta.getAttribute("aria-describedby")).toBeNull()
		expect(document.getElementById("q-locked-reason")?.textContent).toBe("Needed by a child")
	})

	it("describes no locked option when no reason is given", () => {
		renderPicker({ locked: ["a"] })

		fireEvent.click(trigger())

		expect(screen.getAllByRole("checkbox")[0].getAttribute("aria-describedby")).toBeNull()
		expect(document.getElementById("q-locked-reason")).toBeNull()
	})

	it("closes on Escape and returns focus to the trigger, ignoring other keys", () => {
		renderPicker()

		fireEvent.click(trigger())
		fireEvent.keyDown(document, { key: "a" })
		expect(panel()).not.toBeNull()
		fireEvent.keyDown(document, { key: "Escape" })

		expect(panel()).toBeNull()
		expect(document.activeElement).toBe(trigger())
	})

	it("closes when a pointer goes down outside, but not inside", () => {
		renderPicker()

		fireEvent.click(trigger())
		fireEvent.pointerDown(trigger())
		expect(panel()).not.toBeNull()
		fireEvent.pointerDown(document.getElementById("outside") as HTMLElement)
		expect(panel()).toBeNull()
	})

	it("closes when focus moves outside, but not within or to nowhere", () => {
		renderPicker()
		const wrapper = trigger().parentElement?.parentElement as HTMLElement

		fireEvent.click(trigger())
		fireEvent.blur(wrapper, { relatedTarget: null })
		expect(panel()).not.toBeNull()
		fireEvent.blur(wrapper, { relatedTarget: panel() })
		expect(panel()).not.toBeNull()
		fireEvent.blur(wrapper, { relatedTarget: document.getElementById("outside") })
		expect(panel()).toBeNull()
	})
})
