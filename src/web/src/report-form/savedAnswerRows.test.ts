import { describe, expect, it } from "vitest"
import type { PublicOptionView, PublicQuestionView } from "../api/publicQuestions"
import type { DraftAnswer, DraftAttachment } from "./draft"
import { clearedAnswerCount, savedAnswerRows } from "./savedAnswerRows"

const t = (key: string) => key

function option(id: string, labelEn: string, labelFr: string, pin = "none"): PublicOptionView {
	return { id, code: id, labelEn, labelFr, onlyIn: null, pin, aliases: [] }
}

function question(revisionId: string, type: string, extra: Partial<PublicQuestionView> = {}): PublicQuestionView {
	return {
		id: revisionId,
		key: revisionId,
		role: "none",
		revisionId,
		type,
		isRequired: false,
		isPrivate: false,
		allowFutureDates: false,
		displayOrder: 0,
		dependsOnQuestionId: null,
		dependsOnChoiceId: null,
		allowsReporterAdditions: false,
		labelEn: `${revisionId} en`,
		labelFr: `${revisionId} fr`,
		helpTextEn: null,
		helpTextFr: null,
		placeholderEn: null,
		placeholderFr: null,
		options: [],
		children: [],
		...extra,
	}
}

const value = (text: string, extra: Partial<Extract<DraftAnswer, { kind: "value" }>> = {}): DraftAnswer => ({ kind: "value", value: text, ...extra })
const file = (name: string): DraftAttachment => ({ uploadId: name, name, size: 1 })

describe("savedAnswerRows", () => {
	it("lists answers and children in form order, skipping questions that collect no answer", () => {
		const group = question("g", "group", { children: [question("c", "short_text")] })
		const rows = savedAnswerRows([question("s", "statement"), group, question("a", "short_text")], { c: value("child"), a: value("top") }, {}, "en-CA", t)

		expect(rows).toEqual([
			{ revisionId: "c", kind: "answer", label: "c en", value: "child" },
			{ revisionId: "a", kind: "answer", label: "a en", value: "top" },
		])
	})

	it("labels rows in the reader's language", () => {
		const rows = savedAnswerRows([question("a", "short_text")], { a: value("x") }, {}, "fr-CA", t)

		expect(rows[0].label).toBe("a fr")
	})

	it("lists a file-upload question's saved files by name, and nothing when it has none", () => {
		const upload = question("u", "file_upload")
		const other = question("v", "file_upload")

		const rows = savedAnswerRows([upload, other], {}, { u: [file("one.png"), file("two.pdf")] }, "en-CA", t)

		expect(rows).toEqual([{ revisionId: "u", kind: "attachments", label: "u en", value: "one.png, two.pdf" }])
	})

	it("leaves out a question with no saved answer or an empty value answer", () => {
		const rows = savedAnswerRows([question("a", "short_text"), question("b", "short_text")], { b: value("") }, {}, "en-CA", t)

		expect(rows).toEqual([])
	})

	it("keeps an options answer that is empty of text but holds values", () => {
		const q = question("m", "multi_select", { options: [option("1", "One", "Un")] })

		const rows = savedAnswerRows([q], { m: { kind: "options", values: ["1"] } }, {}, "fr-CA", t)

		expect(rows[0].value).toBe("Un")
	})

	it("lists a multi-select's values as the form lists its choices, with an unknown value last as stored", () => {
		const q = question("m", "multi_select", {
			options: [option("z", "Zed", "Zed"), option("a", "Alpha", "Alpha"), option("p", "Pinned", "Pinned", "first")],
		})

		const rows = savedAnswerRows([q], { m: { kind: "options", values: ["gone", "z", "a", "p"] } }, {}, "en-CA", t)

		expect(rows[0].value).toBe("Pinned, Alpha, Zed, gone")
	})

	it("names a single-select's choice, or shows the stored text when no choice matches", () => {
		const q = question("s", "single_select", { options: [option("1", "One", "Un")] })

		expect(savedAnswerRows([q], { s: value("1") }, {}, "fr-CA", t)[0].value).toBe("Un")
		expect(savedAnswerRows([q], { s: value("legacy") }, {}, "en-CA", t)[0].value).toBe("legacy")
	})

	it("shows a phone answer with its calling code, defaulting to Canada", () => {
		const q = question("p", "phone")

		expect(savedAnswerRows([q], { p: value("604 555 1234", { country: "GB" }) }, {}, "en-CA", t)[0].value).toBe("+44 604 555 1234")
		expect(savedAnswerRows([q], { p: value("604 555 1234") }, {}, "en-CA", t)[0].value).toBe("+1 604 555 1234")
	})

	it("formats any other answer for the reader", () => {
		const q = question("d", "yes_no")

		expect(savedAnswerRows([q], { d: value("yes") }, {}, "en-CA", t)[0].value).toBe("report.booleanYes")
	})
})

describe("clearedAnswerCount", () => {
	it("counts held answers and files whose question is not among the rows", () => {
		const rows = [{ revisionId: "kept", kind: "answer" as const, label: "", value: "" }]
		const answers = {
			kept: value("x"),
			gone: value("y"),
			goneOptions: { kind: "options", values: ["1"] } as DraftAnswer,
		}
		const attachments = { goneFiles: [file("a")], keptFiles: [file("b")] }

		expect(clearedAnswerCount(answers, attachments, [...rows, { revisionId: "keptFiles", kind: "attachments", label: "", value: "" }])).toBe(3)
	})

	it("does not count empty answers or an empty file list", () => {
		const answers = { a: value(""), b: { kind: "options", values: [] } as DraftAnswer }

		expect(clearedAnswerCount(answers, { c: [] }, [])).toBe(0)
	})
})
