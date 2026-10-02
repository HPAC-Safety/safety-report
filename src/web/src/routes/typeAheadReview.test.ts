import { describe, expect, it } from "vitest"
import type { TypeAheadValueView } from "../api/adminQuestions"
import { canTranslateDraft, groupByQuestion, wordingIn, type Draft } from "./typeAheadReview"

const draft = (over: Partial<Draft>): Draft => ({
	labelEn: "Cessna",
	labelFr: "",
	baselineEn: "Cessna",
	baselineFr: "",
	direction: "toFrench",
	translating: false,
	translationError: null,
	...over,
})

describe("wordingIn", () => {
	it("reads the reader's language first and falls back to the other", () => {
		expect(wordingIn("en-CA", { labelEn: "Hello", labelFr: "Bonjour" })).toBe("Hello")
		expect(wordingIn("fr-CA", { labelEn: "Hello", labelFr: "Bonjour" })).toBe("Bonjour")
		expect(wordingIn("fr-CA", { labelEn: "Hello", labelFr: null })).toBe("Hello")
		expect(wordingIn("en-CA", { labelEn: null, labelFr: "Bonjour" })).toBe("Bonjour")
		expect(wordingIn("en-CA", { labelEn: null, labelFr: null })).toBe("")
	})
})

describe("canTranslateDraft", () => {
	it("offers Translate when the source has text and the target is empty", () => {
		expect(canTranslateDraft(draft({}))).toBe(true)
		expect(canTranslateDraft(draft({ direction: "toEnglish", labelEn: "", labelFr: "Cessna", baselineFr: "Cessna" }))).toBe(true)
	})

	it("withholds it when the source is blank", () => {
		expect(canTranslateDraft(draft({ labelEn: "  " }))).toBe(false)
	})

	it("withholds it when the target has text and the source is unchanged", () => {
		expect(canTranslateDraft(draft({ labelFr: "Cessna" }))).toBe(false)
	})

	it("offers it again once the source differs from the baseline", () => {
		expect(canTranslateDraft(draft({ labelFr: "Cessna", labelEn: "Cessna 172" }))).toBe(true)
	})
})

describe("groupByQuestion", () => {
	const value = (id: string, questionId: string, labelEn: string, labelFr: string, heading: [string, string]): TypeAheadValueView =>
		({ id, questionId, labelEn, labelFr, questionLabelEn: heading[0], questionLabelFr: heading[1] }) as TypeAheadValueView
	const values = [
		value("1", "q2", "Zulu", "Zoulou", ["Beta", "Bêta"]),
		value("2", "q1", "Alpha", "Alpha", ["Aircraft", "Aéronef"]),
		value("3", "q2", "Able", "Apte", ["Beta", "Bêta"]),
	]
	const wording = (locale: string) => (v: { labelEn: string | null; labelFr: string | null }) => wordingIn(locale, v)

	it("groups by question with the headings and values alphabetical in English", () => {
		const groups = groupByQuestion(values, "en-CA", wording("en-CA"))
		expect(groups.map((group) => [group.heading, group.values.map((v) => v.id)])).toEqual([
			["Aircraft", ["2"]],
			["Beta", ["3", "1"]],
		])
	})

	it("reads the French headings in French", () => {
		const groups = groupByQuestion(values, "fr-CA", wording("fr-CA"))
		expect(groups.map((group) => group.heading)).toEqual(["Aéronef", "Bêta"])
	})
})
