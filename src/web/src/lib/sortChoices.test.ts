import { describe, expect, it } from "vitest"
import { choiceGroups, sortChoices } from "./sortChoices"

type Choice = { id: string; label: string; pin?: string | null }

const label = (choice: Choice) => choice.label

describe("sortChoices", () => {
	it("lists pinned-first choices, then the rest alphabetically, then pinned-last choices", () => {
		const choices: Choice[] = [
			{ id: "c", label: "Other", pin: "last" },
			{ id: "b", label: "Zebra" },
			{ id: "a", label: "Apple", pin: "none" },
			{ id: "d", label: "Canada", pin: "first" },
		]

		expect(sortChoices(choices, "en-CA", label).map((choice) => choice.id)).toEqual(["d", "a", "b", "c"])
	})

	it("ignores accents and case, and compares numbers as numbers", () => {
		const choices: Choice[] = [
			{ id: "1", label: "item 10" },
			{ id: "2", label: "Item 2" },
			{ id: "3", label: "Émeu" },
			{ id: "4", label: "eagle" },
		]

		expect(sortChoices(choices, "fr-CA", label).map((choice) => choice.id)).toEqual(["4", "3", "2", "1"])
	})

	it("breaks a tie by id, whichever order the server sent", () => {
		const choices: Choice[] = [
			{ id: "b", label: "Same" },
			{ id: "c", label: "same" },
			{ id: "a", label: "SAME" },
		]

		expect(sortChoices(choices, "en-CA", label).map((choice) => choice.id)).toEqual(["a", "b", "c"])
		expect(sortChoices([...choices].reverse(), "en-CA", label).map((choice) => choice.id)).toEqual(["a", "b", "c"])
	})

	it("keeps two choices with the same label and id in the order given", () => {
		const choices: Choice[] = [
			{ id: "a", label: "Same" },
			{ id: "a", label: "same" },
		]

		expect(sortChoices(choices, "en-CA", label)).toEqual(choices)
	})

	it("treats a missing, null or unknown pin as unpinned", () => {
		const choices: Choice[] = [
			{ id: "a", label: "A", pin: null },
			{ id: "b", label: "B", pin: "sideways" },
			{ id: "c", label: "C" },
		]

		expect(choiceGroups(choices, "en-CA", label)).toHaveLength(1)
	})

	it("does not change the list it is given", () => {
		const choices: Choice[] = [
			{ id: "b", label: "B" },
			{ id: "a", label: "A" },
		]

		sortChoices(choices, "en-CA", label)

		expect(choices.map((choice) => choice.id)).toEqual(["b", "a"])
	})
})

describe("choiceGroups", () => {
	it("splits the sorted list where the pin group changes", () => {
		const choices: Choice[] = [
			{ id: "1", label: "B", pin: "last" },
			{ id: "2", label: "A", pin: "first" },
			{ id: "3", label: "C" },
			{ id: "4", label: "D" },
		]

		const groups = choiceGroups(choices, "en-CA", label)

		expect(groups.map((group) => group.map((choice) => choice.id))).toEqual([["2"], ["3", "4"], ["1"]])
	})

	it("returns no groups for no choices", () => {
		expect(choiceGroups([], "en-CA", label)).toEqual([])
	})
})
