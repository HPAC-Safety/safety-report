import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { useState } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import type { ListChoice } from "./ChoiceList"
import { SingleSelectField, type SingleSelectFieldProps } from "./SingleSelectField"

const choices = (...labels: string[]): ListChoice[] => labels.map((label) => ({ key: label.toLowerCase(), label, lang: label === "Écureuil" ? "fr" : undefined }))

const groups = [choices("Apple", "Avocado", "Banana"), choices("Écureuil", "Elk")]

function Harness(props: Partial<SingleSelectFieldProps> & { onChanged?: (key: string | undefined) => void }) {
	const [selected, setSelected] = useState<string | undefined>(props.selectedKey)
	return (
		<div>
			<SingleSelectField
				fieldId="q"
				label="Fruit"
				groups={groups}
				placeholder="Choose"
				describedBy="d"
				locale="en-CA"
				{...props}
				selectedKey={selected}
				onChange={(key) => {
					setSelected(key)
					props.onChanged?.(key)
				}}
			/>
			<button id="outside">outside</button>
		</div>
	)
}

const combobox = () => screen.getByRole("combobox")
const list = () => document.getElementById("q-list") as HTMLElement
const isOpen = () => !list().hidden
const press = (key: string, init: KeyboardEventInit = {}) => fireEvent.keyDown(combobox(), { key, ...init })

beforeEach(() => {
	vi.useFakeTimers({ toFake: ["Date"] })
	vi.setSystemTime(new Date("2026-01-01T00:00:00Z"))
	Element.prototype.scrollIntoView = vi.fn()
})

afterEach(() => {
	cleanup()
	vi.useRealTimers()
})

describe("SingleSelectField", () => {
	it("shows the placeholder while unanswered and the chosen label when answered", () => {
		const { unmount } = render(<Harness />)
		expect(combobox().textContent).toBe("Choose")
		expect(combobox().getAttribute("aria-expanded")).toBe("false")
		unmount()

		render(<Harness selectedKey="écureuil" />)
		expect(combobox().textContent).toBe("Écureuil")
		expect(combobox().querySelector("span")?.getAttribute("lang")).toBe("fr")
	})

	it("treats a selected key that matches no choice as unanswered", () => {
		render(<Harness selectedKey="missing" />)
		expect(combobox().textContent).toBe("Choose")
	})

	it("opens on a pointer click at the chosen choice and closes on a second pointer click", () => {
		render(<Harness selectedKey="banana" />)

		fireEvent.click(combobox(), { detail: 1 })
		expect(isOpen()).toBe(true)
		expect(combobox().getAttribute("aria-activedescendant")).toBe("q-option-banana")
		expect(Element.prototype.scrollIntoView).toHaveBeenCalled()

		fireEvent.click(combobox(), { detail: 1 })
		expect(isOpen()).toBe(false)
	})

	it("takes the highlighted choice on Enter or Space, which arrive as a click with no pointer", () => {
		const onChanged = vi.fn()
		render(<Harness onChanged={onChanged} />)

		press("ArrowDown")
		press("ArrowDown")
		fireEvent.click(combobox(), { detail: 0 })

		expect(onChanged).toHaveBeenCalledWith("apple")
		expect(isOpen()).toBe(false)
		expect(document.activeElement).toBe(combobox())
	})

	it("closes without changing when a pointer press lands on the open field", () => {
		const onChanged = vi.fn()
		render(<Harness onChanged={onChanged} />)

		fireEvent.click(combobox(), { detail: 1 })
		expect(isOpen()).toBe(true)
		fireEvent.click(combobox(), { detail: 1 })
		expect(onChanged).not.toHaveBeenCalled()
	})

	it("clears the answer when the placeholder row is chosen", () => {
		const onChanged = vi.fn()
		render(<Harness selectedKey="apple" onChanged={onChanged} />)

		fireEvent.click(combobox(), { detail: 1 })
		fireEvent.click(document.getElementById("q-option-none") as HTMLElement)

		expect(onChanged).toHaveBeenCalledWith(undefined)
	})

	it("chooses a clicked option and follows the pointer with the highlight", () => {
		const onChanged = vi.fn()
		render(<Harness onChanged={onChanged} />)

		fireEvent.click(combobox(), { detail: 1 })
		fireEvent.mouseMove(document.getElementById("q-option-banana") as HTMLElement)
		expect(combobox().getAttribute("aria-activedescendant")).toBe("q-option-banana")
		fireEvent.click(document.getElementById("q-option-banana") as HTMLElement)

		expect(onChanged).toHaveBeenCalledWith("banana")
	})

	it("opens from the closed field with the arrow keys, Home, and End, and ignores other keys", () => {
		render(<Harness selectedKey="avocado" />)

		press("ArrowUp")
		expect(isOpen()).toBe(true)
		expect(combobox().getAttribute("aria-activedescendant")).toBe("q-option-avocado")
		press("Escape")
		expect(isOpen()).toBe(false)

		press("Home")
		expect(combobox().getAttribute("aria-activedescendant")).toBe("q-option-none")
		press("Escape")
		press("End")
		expect(combobox().getAttribute("aria-activedescendant")).toBe("q-option-elk")
		press("Escape")

		press("F2")
		expect(isOpen()).toBe(false)
		press("a", { ctrlKey: true })
		expect(isOpen()).toBe(false)
	})

	it("moves the highlight with the keys while open", () => {
		render(<Harness />)
		press("ArrowDown")
		const active = () => combobox().getAttribute("aria-activedescendant")

		press("ArrowDown")
		expect(active()).toBe("q-option-apple")
		press("ArrowDown", { altKey: true })
		expect(active()).toBe("q-option-apple")
		press("ArrowUp")
		expect(active()).toBe("q-option-none")
		press("ArrowUp")
		expect(active()).toBe("q-option-none")
		press("End")
		expect(active()).toBe("q-option-elk")
		press("ArrowDown")
		expect(active()).toBe("q-option-elk")
		press("Home")
		expect(active()).toBe("q-option-none")
		press("PageDown")
		expect(active()).toBe("q-option-elk")
		press("PageUp")
		expect(active()).toBe("q-option-none")
	})

	it("chooses the highlighted choice on Alt+ArrowUp", () => {
		const onChanged = vi.fn()
		render(<Harness onChanged={onChanged} />)

		press("ArrowDown")
		press("ArrowDown")
		press("ArrowUp", { altKey: true })

		expect(onChanged).toHaveBeenCalledWith("apple")
	})

	it("closes on Escape without letting it reach the page, and on Tab", () => {
		const outer = vi.fn()
		render(
			<div onKeyDown={outer}>
				<Harness />
			</div>,
		)

		press("ArrowDown")
		outer.mockClear()
		press("Escape")
		expect(isOpen()).toBe(false)
		expect(outer).not.toHaveBeenCalled()

		press("ArrowDown")
		press("Tab")
		expect(isOpen()).toBe(false)
	})

	it("ignores a key it has no use for while open", () => {
		render(<Harness />)
		press("ArrowDown")
		press("F2")
		expect(isOpen()).toBe(true)
	})

	it("jumps to the next choice starting with the typed letter, cycling on a repeat, accent and case blind", () => {
		render(<Harness />)
		const active = () => combobox().getAttribute("aria-activedescendant")

		press("a")
		expect(active()).toBe("q-option-apple")
		vi.setSystemTime(Date.now() + 600)
		press("a")
		expect(active()).toBe("q-option-avocado")
		vi.setSystemTime(Date.now() + 600)
		press("A")
		expect(active()).toBe("q-option-apple")
		vi.setSystemTime(Date.now() + 600)
		press("e")
		expect(active()).toBe("q-option-écureuil")
		vi.setSystemTime(Date.now() + 600)
		press("e")
		expect(active()).toBe("q-option-elk")
	})

	it("extends the search while characters arrive within 500 ms, and starts again after", () => {
		render(<Harness />)
		const active = () => combobox().getAttribute("aria-activedescendant")

		press("a")
		vi.setSystemTime(Date.now() + 100)
		press("v")
		expect(active()).toBe("q-option-avocado")
		vi.setSystemTime(Date.now() + 100)
		press(" ")
		expect(active()).toBe("q-option-avocado")

		vi.setSystemTime(Date.now() + 600)
		press("b")
		expect(active()).toBe("q-option-banana")
	})

	it("starts the search from the chosen choice when closed, and leaves the highlight when nothing matches", () => {
		render(<Harness selectedKey="avocado" />)

		press("z")
		expect(isOpen()).toBe(false)

		vi.setSystemTime(Date.now() + 600)
		press("b")
		expect(combobox().getAttribute("aria-activedescendant")).toBe("q-option-banana")
	})

	it("leaves a Space with no search under way to the button", () => {
		render(<Harness />)

		press(" ")
		expect(isOpen()).toBe(false)
	})

	it("forgets a finished search once a choice is made", () => {
		render(<Harness />)

		press("a")
		press("Enter")
		fireEvent.click(combobox(), { detail: 0 })
		expect(isOpen()).toBe(false)
		press(" ")
		expect(isOpen()).toBe(false)
	})

	it("closes when a pointer goes down outside, but not inside", () => {
		render(<Harness />)

		press("ArrowDown")
		fireEvent.pointerDown(combobox())
		expect(isOpen()).toBe(true)
		fireEvent.pointerDown(document.getElementById("outside") as HTMLElement)
		expect(isOpen()).toBe(false)
	})

	it("closes when focus moves outside the field, but not within or to nowhere", () => {
		render(<Harness />)
		const wrapper = combobox().parentElement as HTMLElement

		press("ArrowDown")
		fireEvent.blur(wrapper, { relatedTarget: null })
		expect(isOpen()).toBe(true)
		fireEvent.blur(wrapper, { relatedTarget: list() })
		expect(isOpen()).toBe(true)
		fireEvent.blur(wrapper, { relatedTarget: document.getElementById("outside") })
		expect(isOpen()).toBe(false)
	})

	it("cannot hold its list open while disabled", () => {
		const { rerender } = render(<SingleSelectField fieldId="q" label="Fruit" groups={groups} selectedKey={undefined} placeholder="Choose" describedBy={undefined} locale="en-CA" onChange={() => {}} />)
		fireEvent.click(combobox(), { detail: 1 })
		expect(isOpen()).toBe(true)

		rerender(<SingleSelectField fieldId="q" label="Fruit" groups={groups} selectedKey={undefined} placeholder="Choose" describedBy={undefined} locale="en-CA" onChange={() => {}} disabled />)

		expect(isOpen()).toBe(false)
		expect((combobox() as HTMLButtonElement).disabled).toBe(true)
	})
})
