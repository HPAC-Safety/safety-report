import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"
import {
	Caret,
	ChoiceOptions,
	ChoiceSeparator,
	choiceListClassName,
	choiceRowClassName,
	choiceRowHighlightClassName,
	type ListChoice,
} from "./ChoiceList"

afterEach(cleanup)

const apple: ListChoice = { key: "a", label: "Apple" }
const pear: ListChoice = { key: "p", label: "Pear", lang: "fr", hint: "also: Poire" }

function renderOptions(overrides: Partial<Parameters<typeof ChoiceOptions>[0]> = {}) {
	const onPoint = vi.fn()
	const view = render(
		<ul>
			<ChoiceOptions
				groups={[[apple], [pear]]}
				optionId={(choice) => `opt-${choice.key}`}
				activeKey="a"
				isSelected={(choice) => choice.key === "p"}
				onPoint={onPoint}
				{...overrides}
			/>
		</ul>,
	)
	return { ...view, onPoint }
}

describe("ChoiceList", () => {
	it("exposes the shared class names", () => {
		expect(choiceListClassName).toContain("absolute")
		expect(choiceRowClassName).toContain("touch-target")
		expect(choiceRowHighlightClassName).toContain("bg-surface-4")
	})

	it("draws a decorative caret and separator", () => {
		const { container } = render(
			<ul>
				<Caret />
				<ChoiceSeparator />
			</ul>,
		)
		expect(container.querySelector("svg[aria-hidden='true']")).not.toBeNull()
		expect(container.querySelector("li[data-separator]")).not.toBeNull()
	})

	it("lists each group's options with a separator between groups and a hint on a matched alias", () => {
		const { container } = renderOptions()

		expect(screen.getAllByRole("option")).toHaveLength(2)
		expect(container.querySelectorAll("[data-separator]")).toHaveLength(1)
		expect(screen.getByTestId("choice-hint").textContent).toBe("also: Poire")
		expect(screen.getByText("Pear", { exact: false }).getAttribute("lang")).toBe("fr")
		expect(document.getElementById("opt-a")?.className).toContain(choiceRowHighlightClassName)
		expect(document.getElementById("opt-p")?.getAttribute("aria-selected")).toBe("true")
	})

	it("draws a leading row, marked as the placeholder, before the groups", () => {
		const { container } = renderOptions({ leading: { key: "", label: "Choose" } })

		expect(container.querySelector("[data-placeholder]")?.textContent).toBe("Choose")
		expect(container.querySelectorAll("[data-separator]")).toHaveLength(1)
	})

	it("points at a row it is not already on, and keeps focus out of a pressed row", () => {
		const { onPoint } = renderOptions()
		const [first, second] = screen.getAllByRole("option")

		fireEvent.mouseMove(first)
		expect(onPoint).not.toHaveBeenCalled()
		fireEvent.mouseMove(second)
		expect(onPoint).toHaveBeenCalledWith(pear)

		expect(fireEvent.mouseDown(second)).toBe(false)
	})
})
