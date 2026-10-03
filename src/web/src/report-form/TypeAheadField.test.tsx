import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { useState } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { TYPE_AHEAD_THRESHOLD, TypeAheadField, type TypeAheadChoice } from "./TypeAheadField"

const choice = (key: string, label: string, extra: Partial<TypeAheadChoice> = {}): TypeAheadChoice => ({
	key,
	label,
	lang: undefined,
	...extra,
})

const groups: TypeAheadChoice[][] = [
	[choice("van", "Vancouver", { aliases: [{ labelEn: "Vancity", labelFr: null }, { labelEn: null, labelFr: "Vanville" }] })],
	[choice("vic", "Victoria", { lang: "en" }), choice("vern", "Vernon")],
]

const t = (key: string, params?: Record<string, string | number>) => (params ? `${key}:${params.alias}` : key)

function Harness({ initial = "", initialKey, onValue }: { initial?: string; initialKey?: string; onValue?: (value: string, key?: string) => void }) {
	const [value, setValue] = useState(initial)
	const [key, setKey] = useState<string | undefined>(initialKey)
	return (
		<div>
			<TypeAheadField
				fieldId="q"
				label="City"
				groups={groups}
				value={value}
				selectedKey={key}
				placeholder="Type"
				describedBy="d"
				locale="en-CA"
				t={t}
				onChange={(next, choiceKey) => {
					setValue(next)
					setKey(choiceKey)
					onValue?.(next, choiceKey)
				}}
			/>
			<button id="outside">outside</button>
		</div>
	)
}

const input = () => screen.getByRole<HTMLInputElement>("combobox")
const list = () => document.getElementById("q-list") as HTMLElement
const isOpen = () => !list().hidden
const active = () => input().getAttribute("aria-activedescendant")
const press = (key: string, init: KeyboardEventInit = {}) => fireEvent.keyDown(input(), { key, ...init })
const type = (text: string) => fireEvent.change(input(), { target: { value: text } })
const optionLabels = () => screen.queryAllByRole("option", { hidden: true }).map((option) => option.firstChild?.textContent)

beforeEach(() => {
	Element.prototype.scrollIntoView = vi.fn()
})

afterEach(cleanup)

describe("TypeAheadField", () => {
	it("shows only the hint below the threshold, with no active option", () => {
		expect(TYPE_AHEAD_THRESHOLD).toBe(3)
		render(<Harness />)

		fireEvent.click(input())

		expect(isOpen()).toBe(true)
		expect(list().querySelector("[data-hint]")?.textContent).toBe("report.typeAhead.typeToSeeChoices")
		expect(screen.getByRole("status").textContent).toBe("report.typeAhead.typeToSeeChoices")
		expect(screen.getByRole("status").className).toBe("sr-only")
		press("ArrowDown")
		expect(active()).toBeNull()
	})

	it("narrows the list as the reporter types, matching anywhere, accents and case blind", () => {
		const { container } = render(<Harness />)

		type("RIA")
		expect(optionLabels()).toEqual(["Victoria"])
		expect(container.querySelector("[data-separator]")).toBeNull()

		type("rno")
		expect(optionLabels()).toEqual(["Vernon"])

		type("ver")
		expect(optionLabels()).toEqual(["Vancouver", "Vernon"])
		expect(container.querySelectorAll("[data-separator]")).toHaveLength(1)
	})

	it("offers a choice through a merged alias in either language, with a hint naming it", () => {
		render(<Harness />)

		type("vancit")
		expect(optionLabels()).toEqual(["Vancouver"])
		expect(screen.getByTestId("choice-hint").textContent).toBe("report.typeAhead.alsoKnownAs:Vancity")

		type("vanvil")
		expect(screen.getByTestId("choice-hint").textContent).toBe("report.typeAhead.alsoKnownAs:Vanville")
	})

	it("says so, with a floating box, when nothing fits", () => {
		render(<Harness />)

		type("zzz")

		expect(isOpen()).toBe(false)
		expect(screen.getByRole("status").textContent).toBe("report.typeAhead.noMatches")
		expect(screen.getByRole("status").className).toContain("absolute")
	})

	it("moves the active option with the arrow keys, clamped at both ends, and takes it on Enter", () => {
		const onValue = vi.fn()
		render(<Harness onValue={onValue} />)

		type("ver")
		press("ArrowDown")
		expect(active()).toBe("q-option-van")
		press("ArrowDown")
		expect(active()).toBe("q-option-vern")
		press("ArrowDown")
		expect(active()).toBe("q-option-vern")
		press("ArrowUp")
		expect(active()).toBe("q-option-van")
		press("ArrowUp")
		expect(active()).toBe("q-option-van")
		press("ArrowDown")
		press("Enter")

		expect(onValue).toHaveBeenLastCalledWith("Vernon", "vern")
		expect(input().value).toBe("Vernon")
		expect(isOpen()).toBe(false)
		expect(active()).toBeNull()
	})

	it("starts Up from the end of the list", () => {
		render(<Harness />)

		type("ver")
		press("ArrowUp")

		expect(active()).toBe("q-option-vern")
	})

	it("lets Enter through when no option is active", () => {
		render(<Harness initial="Van" />)

		fireEvent.click(input())

		expect(fireEvent.keyDown(input(), { key: "Enter" })).toBe(true)
	})

	it("opens from the closed field with ArrowDown on the first choice, ArrowUp on the last, and Alt+ArrowDown on none", () => {
		render(<Harness initial="ver" />)

		press("ArrowDown")
		expect(isOpen()).toBe(true)
		expect(active()).toBe("q-option-van")
		press("Escape")

		press("ArrowUp")
		expect(active()).toBe("q-option-vern")
		press("Escape")

		press("ArrowDown", { altKey: true })
		expect(isOpen()).toBe(true)
		expect(active()).toBeNull()
		press("ArrowDown", { altKey: true })
		expect(active()).toBeNull()
	})

	it("opens on the choice the field already holds, by key or by label", () => {
		const { unmount } = render(<Harness initial="Vernon" initialKey="vern" />)
		press("ArrowDown")
		expect(active()).toBe("q-option-vern")
		unmount()

		render(<Harness initial="Vernon" />)
		press("ArrowUp")
		expect(active()).toBe("q-option-vern")
	})

	it("opens on the first choice when the held key is not among those the held text shows", () => {
		render(<Harness initial="ver" initialKey="vic" />)

		press("ArrowDown")

		expect(active()).toBe("q-option-van")
	})

	it("opens a short held text on the hint, with nothing active", () => {
		render(<Harness initial="V" />)

		press("ArrowDown")

		expect(isOpen()).toBe(true)
		expect(active()).toBeNull()
	})

	it("closes on Escape without letting it reach the page, only when open, and on Tab", () => {
		const outer = vi.fn()
		render(
			// eslint-disable-next-line jsx-a11y/no-static-element-interactions -- a probe: it records whether Escape bubbles past the field to the page
			<div onKeyDown={outer}>
				<Harness initial="Van" />
			</div>,
		)

		press("Escape")
		expect(outer).toHaveBeenCalledTimes(1)

		fireEvent.click(input())
		outer.mockClear()
		press("Escape")
		expect(isOpen()).toBe(false)
		expect(outer).not.toHaveBeenCalled()

		fireEvent.click(input())
		press("Tab")
		expect(isOpen()).toBe(false)
	})

	it("ignores a key it has no use for", () => {
		render(<Harness initial="Van" />)

		fireEvent.click(input())
		press("F2")

		expect(isOpen()).toBe(true)
	})

	it("does not reopen on a click while open", () => {
		render(<Harness initial="ver" />)

		press("ArrowDown")
		fireEvent.click(input())

		expect(active()).toBe("q-option-van")
	})

	it("chooses a clicked option and points at the one under the mouse", () => {
		const onValue = vi.fn()
		render(<Harness initial="ver" onValue={onValue} />)

		fireEvent.click(input())
		const option = document.getElementById("q-option-vern") as HTMLElement
		fireEvent.mouseMove(option)
		expect(active()).toBe("q-option-vern")
		fireEvent.click(option)

		expect(onValue).toHaveBeenLastCalledWith("Vernon", "vern")
		expect(document.activeElement).toBe(input())
	})

	it("closes when a pointer goes down outside, but not inside", () => {
		render(<Harness initial="Van" />)

		fireEvent.click(input())
		fireEvent.pointerDown(input())
		expect(isOpen()).toBe(true)
		fireEvent.pointerDown(document.getElementById("outside") as HTMLElement)
		expect(isOpen()).toBe(false)
	})

	it("closes when focus moves outside the field, but not within or to nowhere", () => {
		render(<Harness initial="Van" />)
		const wrapper = input().parentElement as HTMLElement

		fireEvent.click(input())
		fireEvent.blur(wrapper, { relatedTarget: null })
		expect(isOpen()).toBe(true)
		fireEvent.blur(wrapper, { relatedTarget: list() })
		expect(isOpen()).toBe(true)
		fireEvent.blur(wrapper, { relatedTarget: document.getElementById("outside") })
		expect(isOpen()).toBe(false)
	})

	it("can be disabled", () => {
		render(
			<TypeAheadField fieldId="q" label="City" groups={groups} value="" selectedKey={undefined} placeholder={undefined} describedBy={undefined} locale="en-CA" t={t} onChange={() => {}} disabled />,
		)
		expect(input().disabled).toBe(true)
	})
})
