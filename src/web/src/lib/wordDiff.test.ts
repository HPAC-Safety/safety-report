import { describe, expect, it } from "vitest"

import { wordDiff } from "./wordDiff"

describe("wordDiff", () => {
	it("returns no parts for two empty texts", () => {
		expect(wordDiff("", "")).toEqual([])
	})

	it("returns one same part for identical texts", () => {
		expect(wordDiff("the quick fox", "the quick fox")).toEqual([{ kind: "same", text: "the quick fox" }])
	})

	it("marks everything added when the before text is empty", () => {
		expect(wordDiff("", "new words")).toEqual([{ kind: "added", text: "new words" }])
	})

	it("marks everything removed when the after text is empty", () => {
		expect(wordDiff("old words", "")).toEqual([{ kind: "removed", text: "old words" }])
	})

	it("marks a replaced word as removed then added", () => {
		expect(wordDiff("the quick fox", "the slow fox")).toEqual([
			{ kind: "same", text: "the " },
			{ kind: "removed", text: "quick " },
			{ kind: "added", text: "slow " },
			{ kind: "same", text: "fox" },
		])
	})

	it("prefers removing when removal and addition tie", () => {
		expect(wordDiff("a", "b").map((part) => part.kind)).toEqual(["removed", "added"])
	})

	it("takes the added branch when skipping an after word keeps more in common", () => {
		expect(wordDiff("b c", "a b c").map((part) => part.kind)).toEqual(["added", "same"])
	})

	it("merges consecutive parts of the same kind and rebuilds each side", () => {
		const before = "one two three four"
		const after = "one five six four"
		const parts = wordDiff(before, after)
		expect(parts.map((part) => part.kind)).toEqual(["same", "removed", "added", "same"])
		expect(
			parts
				.filter((part) => part.kind !== "added")
				.map((part) => part.text)
				.join(""),
		).toBe(before)
		expect(
			parts
				.filter((part) => part.kind !== "removed")
				.map((part) => part.text)
				.join(""),
		).toBe(after)
	})

	it("keeps whitespace-only texts as tokens", () => {
		expect(wordDiff("  ", "")).toEqual([{ kind: "removed", text: "  " }])
	})
})
