import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { useState } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { EmailField } from "./EmailField"

afterEach(cleanup)

function Harness({ initial = "", onValue }: { initial?: string; onValue?: (value: string) => void }) {
	const [value, setValue] = useState(initial)
	return (
		<EmailField
			fieldId="q"
			className="field"
			describedBy="d"
			placeholder="you@example.com"
			value={value}
			onChange={(next) => {
				setValue(next)
				onValue?.(next)
			}}
			t={(key) => key}
		/>
	)
}

const input = () => screen.getByRole("combobox") as HTMLInputElement
const list = () => document.getElementById("q-suggestions") as HTMLElement
const isOpen = () => !list().hidden
const press = (key: string) => fireEvent.keyDown(input(), { key })
const suggestionTexts = () => Array.from(list().querySelectorAll("li")).map((item) => item.textContent)

describe("EmailField", () => {
	it("is an email combobox whose list is hidden until it has focus and suggestions", () => {
		render(<Harness />)

		expect(input().type).toBe("email")
		expect(input().getAttribute("aria-expanded")).toBe("false")
		expect(list().getAttribute("aria-label")).toBe("report.email.suggestions")

		fireEvent.focus(input())
		fireEvent.change(input(), { target: { value: "sam@" } })

		expect(isOpen()).toBe(true)
		expect(suggestionTexts().length).toBeGreaterThan(1)
		fireEvent.blur(input())
		expect(isOpen()).toBe(false)
	})

	it("chooses a suggestion on press, without moving focus", () => {
		const onValue = vi.fn()
		render(<Harness onValue={onValue} />)

		fireEvent.focus(input())
		fireEvent.change(input(), { target: { value: "sam@" } })
		const [first] = Array.from(list().querySelectorAll("li"))
		expect(fireEvent.mouseDown(first)).toBe(false)

		expect(onValue).toHaveBeenLastCalledWith(first.textContent)
	})

	it("moves through the suggestions with the arrows, wrapping, and takes one on Enter", () => {
		const onValue = vi.fn()
		render(<Harness onValue={onValue} />)
		fireEvent.focus(input())
		fireEvent.change(input(), { target: { value: "sam@" } })
		const items = suggestionTexts()
		const active = () => input().getAttribute("aria-activedescendant")

		press("ArrowUp")
		expect(active()).toBe(`q-suggestions-${items.length - 1}`)
		press("ArrowDown")
		expect(active()).toBe("q-suggestions-0")
		press("ArrowDown")
		expect(active()).toBe("q-suggestions-1")
		press("ArrowUp")
		expect(active()).toBe("q-suggestions-0")
		press("ArrowDown")
		expect(list().querySelectorAll("li")[1].getAttribute("aria-selected")).toBe("true")
		press("Enter")

		expect(onValue).toHaveBeenLastCalledWith(items[1])
		expect(active()).toBeNull()
	})

	it("starts Down on the first suggestion", () => {
		render(<Harness />)
		fireEvent.focus(input())
		fireEvent.change(input(), { target: { value: "sam@" } })

		press("ArrowDown")

		expect(input().getAttribute("aria-activedescendant")).toBe("q-suggestions-0")
	})

	it("leaves Enter alone with nothing highlighted, and ignores other keys", () => {
		render(<Harness />)
		fireEvent.focus(input())
		fireEvent.change(input(), { target: { value: "sam@" } })

		expect(fireEvent.keyDown(input(), { key: "Enter" })).toBe(true)
		expect(fireEvent.keyDown(input(), { key: "x" })).toBe(true)
	})

	it("closes on Escape, and reopens when the reporter arrows or types again", () => {
		render(<Harness />)
		fireEvent.focus(input())
		fireEvent.change(input(), { target: { value: "sam@" } })

		expect(fireEvent.keyDown(input(), { key: "Escape" })).toBe(false)
		expect(isOpen()).toBe(false)
		press("ArrowDown")
		expect(isOpen()).toBe(true)
		press("Escape")
		fireEvent.change(input(), { target: { value: "sam@g" } })
		expect(isOpen()).toBe(true)
	})

	it("leaves Escape and the arrows alone with no suggestions", () => {
		render(<Harness />)
		fireEvent.focus(input())

		expect(fireEvent.keyDown(input(), { key: "ArrowDown" })).toBe(true)
		expect(fireEvent.keyDown(input(), { key: "Escape" })).toBe(true)
	})
})
