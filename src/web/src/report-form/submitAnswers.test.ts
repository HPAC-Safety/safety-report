import { describe, expect, it } from "vitest"

import type { PublicOptionView, PublicQuestionView } from "../api/publicQuestions"
import type { Attachment } from "./AttachmentField"
import { buildSteps, indexQuestionsById, type AnswerMap } from "./steps"
import { buildSubmitAnswers, submittedText } from "./submitAnswers"

function option(id: string, labelEn: string, parentChoiceIds?: string[]): PublicOptionView {
	return { id, code: id, labelEn, labelFr: `${labelEn}-fr`, onlyIn: null, pin: "none", aliases: [], parentChoiceIds }
}

function question(id: string, type: string, extra: Partial<PublicQuestionView> = {}): PublicQuestionView {
	return {
		id,
		key: id,
		role: "none",
		revisionId: `rev-${id}`,
		type,
		isRequired: false,
		isPrivate: false,
		allowFutureDates: false,
		displayOrder: 0,
		dependsOnQuestionId: null,
		dependsOnChoiceId: null,
		allowsReporterAdditions: false,
		labelEn: id,
		labelFr: id,
		helpTextEn: null,
		helpTextFr: null,
		placeholderEn: null,
		placeholderFr: null,
		options: [],
		children: [],
		...extra,
	}
}

function build(top: PublicQuestionView[], answers: AnswerMap, attachments: Record<string, Attachment[]> = {}) {
	const steps = buildSteps(top)
	return buildSubmitAnswers(steps, answers, attachments, indexQuestionsById(top), false)
}

describe("submittedText", () => {
	it("sends any other text as typed", () => {
		expect(submittedText(question("a", "short_text"), { value: "  as typed " })).toBe("  as typed ")
	})

	it("trims an email address and sends a blank one as no answer", () => {
		expect(submittedText(question("a", "email"), { value: " pilot@example.com " })).toBe("pilot@example.com")
		expect(submittedText(question("a", "email"), { value: "   " })).toBeNull()
	})

	it("sends a phone number in E.164 for its country, and a blank one as no answer", () => {
		expect(submittedText(question("a", "phone"), { value: "604 555 1234", country: "CA" })).toBe("+16045551234")
		expect(submittedText(question("a", "phone"), { value: "604 555 1234" })).toBe("+16045551234")
		expect(submittedText(question("a", "phone"), { value: " " })).toBeNull()
	})

	it("falls back to what was entered when a phone number does not parse", () => {
		expect(submittedText(question("a", "phone"), { value: "abc", country: "CA" })).toBe("abc")
	})
})

describe("buildSubmitAnswers", () => {
	it("sends text, a yes/no as a boolean, and an unanswered question as null", () => {
		const top = [question("text", "short_text"), question("yes", "yes_no"), question("check", "checkbox"), question("none", "short_text"), question("odd", "yes_no")]
		const answers: AnswerMap = {
			"rev-text": { kind: "value", value: "hello" },
			"rev-yes": { kind: "value", value: "yes" },
			"rev-check": { kind: "value", value: "no" },
			"rev-odd": { kind: "value", value: "maybe" },
		}

		expect(build(top, answers)).toEqual([
			{ questionRevisionId: "rev-text", value: "hello", choices: null, attachments: null },
			{ questionRevisionId: "rev-yes", value: true, choices: null, attachments: null },
			{ questionRevisionId: "rev-check", value: false, choices: null, attachments: null },
			{ questionRevisionId: "rev-none", value: null, choices: null, attachments: null },
			{ questionRevisionId: "rev-odd", value: null, choices: null, attachments: null },
		])
	})

	it("skips statements and groups themselves but asks a visible group's children", () => {
		const top = [
			question("intro", "statement"),
			question("grp", "group", { children: [question("kid", "short_text"), question("note", "statement")] }),
		]

		expect(build(top, { "rev-kid": { kind: "value", value: "x" } })).toEqual([
			{ questionRevisionId: "rev-kid", value: "x", choices: null, attachments: null },
		])
	})

	it("names single- and multi-select choices by ID and drops values no choice carries", () => {
		const choices = [option("o1", "One"), option("o2", "Two")]
		const top = [question("single", "single_select", { options: choices }), question("multi", "multi_select", { options: choices }), question("blank", "single_select", { options: choices })]
		const answers: AnswerMap = {
			"rev-single": { kind: "value", value: "o1" },
			"rev-multi": { kind: "options", values: ["Two", "nope"] },
		}

		expect(build(top, answers)).toEqual([
			{ questionRevisionId: "rev-single", value: null, choices: ["o1"], attachments: null },
			{ questionRevisionId: "rev-multi", value: null, choices: ["o2"], attachments: null },
			{ questionRevisionId: "rev-blank", value: null, choices: [], attachments: null },
		])
	})

	it("sends a picked type-ahead choice by ID, matched typed text by ID, and unknown typed text as the text", () => {
		const choices = [option("c1", "Cessna"), option("c2", "Piper")]
		const top = [
			question("picked", "autocomplete", { options: choices }),
			question("typed", "autocomplete", { options: choices }),
			question("added", "autocomplete", { options: choices }),
			question("empty", "autocomplete", { options: choices }),
			question("stale", "autocomplete", { options: choices }),
		]
		const answers: AnswerMap = {
			"rev-picked": { kind: "value", value: "whatever", choice: "c2" },
			"rev-typed": { kind: "value", value: " cessna " },
			"rev-added": { kind: "value", value: "  Beech  " },
			"rev-stale": { kind: "value", value: "Piper", choice: "gone" },
		}

		expect(build(top, answers)).toEqual([
			{ questionRevisionId: "rev-picked", value: null, choices: ["c2"], attachments: null },
			{ questionRevisionId: "rev-typed", value: null, choices: ["c1"], attachments: null },
			{ questionRevisionId: "rev-added", value: "Beech", choices: null, attachments: null },
			{ questionRevisionId: "rev-empty", value: null, choices: [], attachments: null },
			{ questionRevisionId: "rev-stale", value: null, choices: ["c2"], attachments: null },
		])
	})

	it("matches choices only among those the parent's answer offers, and skips what cannot be answered yet", () => {
		const parent = question("make", "single_select", { options: [option("m1", "Cessna")] })
		const child = question("model", "single_select", {
			choicesDependOnQuestionId: "make",
			options: [option("d1", "172", ["m1"]), option("d2", "182", ["other"])],
		})
		const top = [parent, child]
		const steps = buildSteps(top)
		const byId = indexQuestionsById(top)

		const waiting = buildSubmitAnswers(steps, {}, {}, byId, false)
		expect(waiting.map((answer) => answer.questionRevisionId)).toEqual(["rev-make"])

		const answered = buildSubmitAnswers(steps, { "rev-make": { kind: "value", value: "m1" }, "rev-model": { kind: "value", value: "d2" } }, {}, byId, false)
		expect(answered[1]).toEqual({ questionRevisionId: "rev-model", value: null, choices: [], attachments: null })
	})

	it("sends a file-upload question's finished uploads by ID and name", () => {
		const top = [question("files", "file_upload"), question("nofiles", "file_upload")]
		const attachments: Record<string, Attachment[]> = {
			"rev-files": [
				{ key: "1", name: "a.pdf", size: 1, status: "uploaded", uploadId: "u1" },
				{ key: "2", name: "b.pdf", size: 1, status: "rejected", uploadId: "u2", reason: "unknown" },
				{ key: "3", name: "c.pdf", size: 1, status: "uploaded" },
			],
		}

		expect(build(top, {}, attachments)).toEqual([
			{ questionRevisionId: "rev-files", value: null, choices: null, attachments: [{ uploadId: "u1", fileName: "a.pdf" }] },
			{ questionRevisionId: "rev-nofiles", value: null, choices: null, attachments: [] },
		])
	})
})
