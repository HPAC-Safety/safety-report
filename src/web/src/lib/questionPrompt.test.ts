import { describe, expect, it } from "vitest"

import { endsWithColon, labelWithColon } from "./questionPrompt"

describe("labelWithColon", () => {
	it("adds a colon in en-CA", () => {
		expect(labelWithColon("Name", "text", "en-CA")).toBe("Name:")
	})

	it("adds a no-break space and a colon in fr-CA", () => {
		expect(labelWithColon("Nom", "text", "fr-CA")).toBe("Nom :")
	})

	it("adds none to a statement or a group", () => {
		expect(labelWithColon("Note", "statement", "en-CA")).toBe("Note")
		expect(labelWithColon("Aircraft", "group", "fr-CA")).toBe("Aircraft")
	})

	it("adds none after a question mark, even with trailing space", () => {
		expect(labelWithColon("Injured?", "yes_no", "en-CA")).toBe("Injured?")
		expect(labelWithColon("Blessé ? ", "yes_no", "fr-CA")).toBe("Blessé ? ")
	})
})

describe("endsWithColon", () => {
	it("detects a closing colon, ignoring trailing whitespace", () => {
		expect(endsWithColon("Name:")).toBe(true)
		expect(endsWithColon("Name:  ")).toBe(true)
	})

	it("is false otherwise", () => {
		expect(endsWithColon("Name")).toBe(false)
		expect(endsWithColon("Name: first")).toBe(false)
		expect(endsWithColon("")).toBe(false)
	})
})
