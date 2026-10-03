import { describe, expect, it } from "vitest"
import { optionRowId } from "./optionRowId"

function listbox(): HTMLElement {
	const list = document.createElement("ul")
	list.innerHTML = '<li role="option" id="o-1">One <span data-hint>also</span></li><li role="presentation" id="hint">Type more</li><li role="option">No id</li>'
	return list
}

describe("optionRowId", () => {
	it("names the option row a click on the row itself reached", () => {
		expect(optionRowId(listbox().children[0])).toBe("o-1")
	})

	it("names the row a click on something inside it reached", () => {
		expect(optionRowId(listbox().querySelector("[data-hint]") as Element)).toBe("o-1")
	})

	it("names nothing for a row that is not an option, or an option with no id", () => {
		const list = listbox()
		expect(optionRowId(list.children[1])).toBeUndefined()
		expect(optionRowId(list.children[2])).toBeUndefined()
		expect(optionRowId(document.createElement("button"))).toBeUndefined()
	})

	it("names nothing for a target that is not an element", () => {
		expect(optionRowId(document.createTextNode("text"))).toBeUndefined()
	})
})
