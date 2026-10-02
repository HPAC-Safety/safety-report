import { describe, expect, it } from "vitest"
import type { ReportAnswer, ReportAnswerValue } from "../api/adminReports"
import { listedValues } from "./reportAnswers"

const value = (text: string, over: Partial<ReportAnswerValue> = {}): ReportAnswerValue => ({
	value: text,
	locale: "en-CA",
	translatedValue: null,
	translationSource: null,
	pin: null,
	...over,
})
const answer = (type: string, values: ReportAnswerValue[]): ReportAnswer => ({
	questionKey: "k",
	labelEn: "L",
	labelFr: "L",
	type,
	isPrivate: false,
	values,
})

describe("listedValues", () => {
	it("keeps the values of any answer but a multi-select as stored", () => {
		const values = [value("b"), value("a")]

		expect(listedValues(answer("short_text", values), "en-CA")).toBe(values)
	})

	it("lists a multi-select's values alphabetically in the reader's language", () => {
		const listed = listedValues(answer("multi_select", [value("Zebra"), value("apple")]), "en-CA")

		expect(listed.map((item) => item.value)).toEqual(["apple", "Zebra"])
	})

	it("reads a value in the other language through its translation", () => {
		const listed = listedValues(
			answer("multi_select", [value("Zèbre", { locale: "fr-CA", translatedValue: "Zebra" }), value("Cat")]),
			"en-CA",
		)

		expect(listed.map((item) => item.value)).toEqual(["Cat", "Zèbre"])
	})

	it("falls back to the stored value when a value has no translation", () => {
		const listed = listedValues(answer("multi_select", [value("b", { locale: "fr-CA" }), value("a")]), "en-CA")

		expect(listed.map((item) => item.value)).toEqual(["a", "b"])
	})

	it("lists pinned-first values before the rest and pinned-last values after", () => {
		const listed = listedValues(
			answer("multi_select", [value("Other", { pin: "last" }), value("Apple"), value("Canada", { pin: "first" })]),
			"en-CA",
		)

		expect(listed.map((item) => item.value)).toEqual(["Canada", "Apple", "Other"])
	})
})
