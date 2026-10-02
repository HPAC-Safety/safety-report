import { describe, expect, it } from "vitest"
import type { ImportedQuestionDraftView } from "../api/adminTypeformImport"
import type { OptionView, QuestionView } from "../api/adminQuestions"
import {
	blankDraft,
	canTranslateChoice,
	choiceLabel,
	choiceRow,
	draftFromImported,
	draftOf,
	sourceOf,
	wordingFieldsToTranslate,
	wordingLabels,
	wordingOf,
	wordingSides,
} from "./questionDraft"

function option(over: Partial<OptionView> & { id: string }): OptionView {
	return {
		code: over.id,
		labelEn: over.id,
		labelFr: over.id,
		addedByReporter: false,
		needsTranslation: false,
		reporterLocale: null,
		pin: "none",
		parentChoiceIds: [],
		...over,
	}
}

function question(over: Partial<QuestionView> & { id: string }): QuestionView {
	return {
		key: over.id,
		revisionId: "rev",
		revisionNumber: 1,
		type: "short_text",
		isSystem: false,
		isRequired: false,
		isPrivate: true,
		isTranslatable: false,
		allowFutureDates: false,
		isActive: true,
		displayOrder: 1,
		dependsOnQuestionId: null,
		dependsOnChoiceId: null,
		groupedUnderQuestionId: null,
		choicesDependOnQuestionId: null,
		labelEn: "English",
		labelFr: "French",
		helpTextEn: null,
		helpTextFr: null,
		placeholderEn: null,
		placeholderFr: null,
		options: [],
		reporterChoicesAwaitingReview: 0,
		hasBeenAnswered: false,
		...over,
	}
}

function imported(over: Partial<ImportedQuestionDraftView> = {}): ImportedQuestionDraftView {
	return {
		key: "k",
		type: "short_text",
		labelEn: "En",
		labelFr: "Fr",
		frenchDefaultedToEnglish: false,
		helpTextEn: null,
		helpTextFr: null,
		groupedUnderKey: null,
		options: [],
		isPrivate: true,
		isRequired: false,
		dependsOnKey: null,
		dependsOnOptionCode: null,
		allowFutureDates: false,
		...over,
	}
}

describe("blankDraft", () => {
	it("is a private, optional, active short-text question with no key and no choices", () => {
		const { request } = blankDraft()

		expect(request).toMatchObject({ type: "short_text", isPrivate: true, isRequired: false, isActive: true, options: [] })
		expect(request.key).toBeUndefined()
		expect(request.isTranslatable).toBe(false)
	})
})

describe("choiceLabel", () => {
	it("prefers the reader's language and falls back to the other", () => {
		expect(choiceLabel({ labelEn: "Hi", labelFr: "Salut" }, "en-CA")).toBe("Hi")
		expect(choiceLabel({ labelEn: "Hi", labelFr: "Salut" }, "fr-CA")).toBe("Salut")
		expect(choiceLabel({ labelEn: null, labelFr: "Salut" }, "en-CA")).toBe("Salut")
		expect(choiceLabel({ labelEn: "Hi", labelFr: "" }, "fr-CA")).toBe("Hi")
		expect(choiceLabel({ labelEn: null, labelFr: null }, "en-CA")).toBe("")
	})
})

describe("draftOf", () => {
	it("copies the question and lists its choices sorted for the locale, with a missing language empty", () => {
		const draft = draftOf(
			question({
				id: "q",
				helpTextEn: "help",
				options: [
					option({ id: "b", labelEn: "Zebra", labelFr: "Zèbre", addedByReporter: true, pin: "last", parentChoiceIds: ["p"] }),
					option({ id: "a", labelEn: null, labelFr: "Abeille" }),
				],
			}),
			"fr-CA",
		)

		expect(draft.request.helpTextEn).toBe("help")
		expect(draft.request.options).toEqual([
			{ code: "a", labelEn: "", labelFr: "Abeille", addedByReporter: false, pin: "none", parentChoiceIds: [] },
			{ code: "b", labelEn: "Zebra", labelFr: "Zèbre", addedByReporter: true, pin: "last", parentChoiceIds: ["p"] },
		])
	})

	it("sorts a French-less choice by its English wording for an English reader", () => {
		const draft = draftOf(
			question({ id: "q", options: [option({ id: "b", labelEn: "B", labelFr: null }), option({ id: "a", labelEn: "A", labelFr: "A" })] }),
			"en-CA",
		)

		expect(draft.request.options.map((choice) => choice.code)).toEqual(["a", "b"])
		expect(draft.request.options[1]?.labelFr).toBe("")
	})
})

describe("draftFromImported", () => {
	it("carries a plain field, naming no group, parent or choice parent", () => {
		const { request } = draftFromImported(imported({ type: "long_text", allowFutureDates: true }), [])

		expect(request).toMatchObject({
			key: "k",
			type: "long_text",
			isTranslatable: true,
			allowFutureDates: true,
			isActive: true,
			dependsOnQuestionId: null,
			dependsOnChoiceId: null,
			groupedUnderQuestionId: null,
			choicesDependOnQuestionId: null,
			options: [],
		})
	})

	it("resolves the group, the condition and its option code against the live questions", () => {
		const live = [
			question({ id: "g", key: "grp", type: "group" }),
			question({ id: "d", key: "dep", type: "single_select", options: [option({ id: "yes-id", code: "yes" })] }),
		]

		const { request } = draftFromImported(
			imported({ groupedUnderKey: "grp", dependsOnKey: "dep", dependsOnOptionCode: "yes" }),
			live,
		)

		expect(request.groupedUnderQuestionId).toBe("g")
		expect(request.dependsOnQuestionId).toBe("d")
		expect(request.dependsOnChoiceId).toBe("yes-id")
	})

	it("leaves a reference to an unknown key or option code empty", () => {
		const live = [question({ id: "d", key: "dep", options: [] })]

		const { request } = draftFromImported(
			imported({ groupedUnderKey: "nope", dependsOnKey: "dep", dependsOnOptionCode: "missing" }),
			live,
		)

		expect(request.groupedUnderQuestionId).toBeNull()
		expect(request.dependsOnQuestionId).toBe("d")
		expect(request.dependsOnChoiceId).toBeNull()
	})

	it("names each option's parent choices by ID when the choices depend on a question", () => {
		const live = [question({ id: "p", key: "prov", options: [option({ id: "bc-id", code: "bc" }), option({ id: "ab-id", code: "ab" })] })]

		const { request } = draftFromImported(
			imported({
				choicesDependOnKey: "prov",
				options: [
					{ code: "van", labelEn: "Vancouver", labelFr: "Vancouver", frenchDefaultedToEnglish: false, parentRefs: ["bc", "gone"] },
					{ code: "any", labelEn: "Anywhere", labelFr: "N'importe où", frenchDefaultedToEnglish: false },
				],
			}),
			live,
		)

		expect(request.choicesDependOnQuestionId).toBe("p")
		expect(request.options).toEqual([
			{ code: "van", labelEn: "Vancouver", labelFr: "Vancouver", parentChoiceIds: ["bc-id"] },
			{ code: "any", labelEn: "Anywhere", labelFr: "N'importe où", parentChoiceIds: [] },
		])
	})

	it("adds no parent links when the choices depend on nothing", () => {
		const { request } = draftFromImported(
			imported({ options: [{ code: "a", labelEn: "A", labelFr: "A", frenchDefaultedToEnglish: false, parentRefs: ["x"] }] }),
			[],
		)

		expect(request.options).toEqual([{ code: "a", labelEn: "A", labelFr: "A" }])
	})
})

describe("choice translation", () => {
	it("starts a row clean, remembering both wordings", () => {
		expect(choiceRow({ labelEn: "A", labelFr: "B" }, 3)).toEqual({ key: 3, baselineEn: "A", baselineFr: "B", pending: false, error: null })
	})

	it("reads the source in the direction translated from", () => {
		const choice = { labelEn: "A", labelFr: "B" }

		expect(sourceOf(choice, "toFrench")).toBe("A")
		expect(sourceOf(choice, "toEnglish")).toBe("B")
	})

	it("offers Translate when the target is empty, or the source was edited, and the source has text", () => {
		const row = choiceRow({ labelEn: "A", labelFr: "B" }, 1)
		const both = { code: null, labelEn: "A", labelFr: "B" }

		expect(canTranslateChoice(both, row, "toFrench")).toBe(false)
		expect(canTranslateChoice(both, row, "toEnglish")).toBe(false)
		expect(canTranslateChoice({ ...both, labelEn: "A2" }, row, "toFrench")).toBe(true)
		expect(canTranslateChoice({ ...both, labelFr: "B2" }, row, "toEnglish")).toBe(true)
		expect(canTranslateChoice({ ...both, labelFr: " " }, row, "toFrench")).toBe(true)
		expect(canTranslateChoice({ ...both, labelEn: " ", labelFr: "" }, row, "toFrench")).toBe(false)
	})
})

describe("wording translation", () => {
	const request = { ...blankDraft().request, labelEn: "Label", labelFr: "", helpTextEn: "Help", helpTextFr: "Aide" }

	it("reads the four wording fields, an absent help text as empty", () => {
		expect(wordingOf({ ...request, helpTextEn: null, helpTextFr: null })).toEqual({
			labelEn: "Label",
			labelFr: "",
			helpTextEn: "",
			helpTextFr: "",
		})
	})

	it("names the source and target side of a direction", () => {
		expect(wordingSides("toFrench")).toEqual({ source: "En", target: "Fr" })
		expect(wordingSides("toEnglish")).toEqual({ source: "Fr", target: "En" })
	})

	it("lists the fields whose target is empty or whose source was edited", () => {
		const wording = wordingOf(request)

		expect(wordingFieldsToTranslate(wording, wording, "toFrench")).toEqual(["label"])
		expect(wordingFieldsToTranslate(wording, { ...wording, helpTextEn: "Old" }, "toFrench")).toEqual(["label", "helpText"])
		expect(wordingFieldsToTranslate(wording, wording, "toEnglish")).toEqual([])
	})

	it("labels a statement's fields as a title and a description", () => {
		expect(wordingLabels("statement")).toEqual({ labelEn: "titleEn", labelFr: "titleFr", helpEn: "descriptionEn", helpFr: "descriptionFr" })
		expect(wordingLabels("short_text")).toEqual({ labelEn: "labelEn", labelFr: "labelFr", helpEn: "helpEn", helpFr: "helpFr" })
	})
})
