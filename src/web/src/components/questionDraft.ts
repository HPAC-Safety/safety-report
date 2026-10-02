import { translatableByDefault, type OptionInput, type QuestionType, type QuestionView, type SaveQuestionRequest } from "../api/adminQuestions"
import type { ImportedQuestionDraftView } from "../api/adminTypeformImport"
import type { Locale } from "../i18n/locales"
import { sortChoices } from "../lib/sortChoices"
import type { TranslationDirection } from "./TranslationDirectionSwitch"

/*
 * The question editor's pure helpers (ADR-0188): the draft a question opens as,
 * and the rules for when a Translate button is offered. No state, no network.
 * The view imports its types from here, because a view imports nothing from
 * api/.
 */

export type { ImportedQuestionDraftView, OptionInput, QuestionType, QuestionView, SaveQuestionRequest }

export interface QuestionDraft {
	request: SaveQuestionRequest
}

export function blankDraft(): QuestionDraft {
	return {
		request: {
			// No key: the API derives one from the English wording, and an
			// administrator never sees or chooses it.
			type: "short_text",
			labelEn: "",
			labelFr: "",
			helpTextEn: null,
			helpTextFr: null,
			placeholderEn: null,
			placeholderFr: null,
			isRequired: false,
			// Private by default: a question whose answers could identify
			// someone is the safe assumption, and an administrator opts out
			// deliberately. See ADR-0038.
			isPrivate: true,
			isTranslatable: translatableByDefault("short_text"),
			allowFutureDates: false,
			isActive: true,
			dependsOnQuestionId: null,
			dependsOnChoiceId: null,
			groupedUnderQuestionId: null,
			choicesDependOnQuestionId: null,
			options: [],
		},
	}
}

/** The wording a reader in `locale` sees for a choice: their language, or the one it has. */
export function choiceLabel(option: { labelEn: string | null; labelFr: string | null }, locale: Locale): string {
	return (locale === "fr-CA" ? (option.labelFr || option.labelEn) : (option.labelEn || option.labelFr)) ?? ""
}

/**
 * The editor's draft of a question. Its options are listed as the form lists
 * them — pinned first, then alphabetically in `locale`, then pinned last — once,
 * here, when the question is opened, and never re-sorted while the
 * administrator edits (ADR-0136).
 */
export function draftOf(question: QuestionView, locale: Locale): QuestionDraft {
	return {
		request: {
			type: question.type,
			labelEn: question.labelEn,
			labelFr: question.labelFr,
			helpTextEn: question.helpTextEn,
			helpTextFr: question.helpTextFr,
			placeholderEn: question.placeholderEn,
			placeholderFr: question.placeholderFr,
			isRequired: question.isRequired,
			isPrivate: question.isPrivate,
			isTranslatable: question.isTranslatable,
			allowFutureDates: question.allowFutureDates,
			isActive: question.isActive,
			dependsOnQuestionId: question.dependsOnQuestionId,
			dependsOnChoiceId: question.dependsOnChoiceId,
			groupedUnderQuestionId: question.groupedUnderQuestionId,
			choicesDependOnQuestionId: question.choicesDependOnQuestionId,
			options: sortChoices(question.options, locale, (option) => choiceLabel(option, locale)).map((option) => ({
				code: option.code,
				// A reporter-added choice may be missing one language; the field
				// shows empty and the server keeps it missing until it is filled.
				labelEn: option.labelEn ?? "",
				labelFr: option.labelFr ?? "",
				addedByReporter: option.addedByReporter,
				pin: option.pin,
				parentChoiceIds: option.parentChoiceIds,
			})),
		},
	}
}

/**
 * An imported field references its group parent by Typeform key, not a
 * database id — the parent may not exist yet if it hasn't been reviewed and
 * saved. Resolved against the live question list at the moment the draft is
 * opened for review.
 */
export function draftFromImported(imported: ImportedQuestionDraftView, questions: QuestionView[]): QuestionDraft {
	const group = imported.groupedUnderKey
		? questions.find((question) => question.key === imported.groupedUnderKey)
		: undefined
	const dependsOn = imported.dependsOnKey
		? questions.find((question) => question.key === imported.dependsOnKey)
		: undefined
	// The export names the parent question by key and each parent choice by code
	// (ADR-0151); the editor names both by ID.
	const choiceParent = imported.choicesDependOnKey
		? questions.find((question) => question.key === imported.choicesDependOnKey)
		: undefined

	return {
		request: {
			key: imported.key,
			type: imported.type,
			labelEn: imported.labelEn,
			labelFr: imported.labelFr,
			helpTextEn: imported.helpTextEn,
			helpTextFr: imported.helpTextFr,
			placeholderEn: null,
			placeholderFr: null,
			isRequired: imported.isRequired,
			isPrivate: imported.isPrivate,
			isTranslatable: translatableByDefault(imported.type),
			// Carried in the export's hpac object; a plain Typeform file has none (ADR-0138).
			allowFutureDates: imported.allowFutureDates,
			isActive: true,
			dependsOnQuestionId: dependsOn?.id ?? null,
			// The Typeform file names the required option by code; the editor names
			// the parent's choice by ID (ADR-0128).
			dependsOnChoiceId: dependsOn?.options.find((option) => option.code === imported.dependsOnOptionCode)?.id ?? null,
			groupedUnderQuestionId: group?.id ?? null,
			choicesDependOnQuestionId: choiceParent?.id ?? null,
			options: imported.options.map((option) => ({
				code: option.code,
				labelEn: option.labelEn,
				labelFr: option.labelFr,
				...(choiceParent
					? {
							parentChoiceIds: (option.parentRefs ?? [])
								.map((code) => choiceParent.options.find((choice) => choice.code === code)?.id)
								.filter((id): id is string => id !== undefined),
						}
					: {}),
			})),
		},
	}
}

// The field a choice is translated from: marked just enough that the direction
/**
 * What the editor remembers about one choice row, beside the draft: a stable
 * identity (a new choice has no code until it is saved), the wording its
 * Translate compares against, and that row's own request state.
 */
export interface ChoiceRow {
	key: number
	baselineEn: string
	baselineFr: string
	pending: boolean
	error: string | null
}

export function choiceRow(option: Pick<OptionInput, "labelEn" | "labelFr">, key: number): ChoiceRow {
	return { key, baselineEn: option.labelEn, baselineFr: option.labelFr, pending: false, error: null }
}

export function sourceOf(option: Pick<OptionInput, "labelEn" | "labelFr">, direction: TranslationDirection): string {
	return direction === "toFrench" ? option.labelEn : option.labelFr
}

/**
 * A choice's Translate is offered when its source wording has text and either
 * its other language is empty, or its source differs from what it was when the
 * editor opened or when the choice was last translated. A choice written in
 * both languages that nobody has edited offers nothing (ADR-0141).
 */
export function canTranslateChoice(option: OptionInput, row: ChoiceRow, direction: TranslationDirection): boolean {
	const source = sourceOf(option, direction)
	const target = direction === "toFrench" ? option.labelFr : option.labelEn
	const baseline = direction === "toFrench" ? row.baselineEn : row.baselineFr
	return source.trim().length > 0 && (target.trim().length === 0 || source !== baseline)
}

/**
 * The wording fields Translate works on, as the request names them without
 * their language. The placeholder has no field in this editor, so Translate
 * never writes one: nobody would see the draft before saving it (ADR-0144).
 */
const WORDING_FIELDS = ["label", "helpText"] as const
export type WordingField = (typeof WORDING_FIELDS)[number]
export type Wording = Record<`${WordingField}${"En" | "Fr"}`, string>

export function wordingOf(request: SaveQuestionRequest): Wording {
	return {
		labelEn: request.labelEn,
		labelFr: request.labelFr,
		helpTextEn: request.helpTextEn ?? "",
		helpTextFr: request.helpTextFr ?? "",
	}
}

export function wordingSides(direction: TranslationDirection) {
	return direction === "toFrench" ? ({ source: "En", target: "Fr" } as const) : ({ source: "Fr", target: "En" } as const)
}

/**
 * A wording field needs translating on the same rule as a choice: its source
 * has text, and either its target is empty or its source differs from what it
 * was when the editor opened or when it was last translated (ADR-0144).
 */
function wordingFieldNeedsTranslation(wording: Wording, baseline: Wording, field: WordingField, direction: TranslationDirection): boolean {
	const { source, target } = wordingSides(direction)
	const text = wording[`${field}${source}`]
	return text.trim().length > 0 && (wording[`${field}${target}`].trim().length === 0 || text !== baseline[`${field}${source}`])
}

/** The wording fields Translate would replace now; it is offered while there is one. */
export function wordingFieldsToTranslate(wording: Wording, baseline: Wording, direction: TranslationDirection): WordingField[] {
	return WORDING_FIELDS.filter((field) => wordingFieldNeedsTranslation(wording, baseline, field, direction))
}
/**
 * The wording fields' labels for a type. A statement is instructional text: a
 * title and a description, not a question and help text. Both still save to
 * the revision's label and help-text fields (REQ-QB-141).
 */
export function wordingLabels(type: QuestionType) {
	return type === "statement"
		? { labelEn: "titleEn", labelFr: "titleFr", helpEn: "descriptionEn", helpFr: "descriptionFr" }
		: { labelEn: "labelEn", labelFr: "labelFr", helpEn: "helpEn", helpFr: "helpFr" }
}
