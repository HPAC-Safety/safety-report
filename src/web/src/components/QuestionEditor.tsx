import { useEffect, useId, useLayoutEffect, useRef, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import {
	ApiError,
	NO_ANSWER_TYPES,
	OPTION_TYPES,
	TRANSLATABLE_TYPES,
	translatableByDefault,
	QUESTION_TYPES,
	translate,
} from "../api/adminQuestions"
import { endsWithColon } from "../lib/questionPrompt"
import { sortChoices } from "../lib/sortChoices"
import { DEFAULT_TRANSLATION_DIRECTION, translationLocales, type TranslationDirection } from "./TranslationDirectionSwitch"
import {
	canTranslateChoice,
	choiceLabel,
	choiceRow,
	sourceOf,
	wordingFieldsToTranslate,
	wordingLabels,
	wordingOf,
	wordingSides,
	type ChoiceRow,
	type OptionInput,
	type QuestionType,
	type SaveQuestionRequest,
	type Wording,
	type WordingField,
} from "./questionDraft"
import {
	QuestionEditorView,
	type ChoiceItem,
	type QuestionEditorProps,
	type QuestionEditorViewModel,
} from "./QuestionEditor.view"

/*
 * The authoring form for one question.
 *
 * Both official languages are authored here, together, because a revision is
 * born complete — there is no partially translated question in the database,
 * and Save stays disabled until both languages are present.
 *
 * Translate drafts the other language of the wording, in the direction its
 * switch shows (ADR-0144). It is a drafting aid, not a pipeline: the result
 * lands in an ordinary editable field, the administrator corrects it, and what
 * they save is theirs. Nothing records that a machine
 * suggested it, because the person who pressed Save is accountable for the
 * wording either way (ADR-0062). The call goes to our own API — the credential
 * never reaches this page.
 *
 * Each choice is translated on its own, when the administrator asks. The
 * direction switch at the top of the Choices panel says which way every
 * choice's Translate goes, English to French by default. A choice's Translate
 * is offered when its other language is empty, or once its source wording has
 * been edited since the editor opened or since that choice was last translated.
 * It replaces the other language with an editable draft, and nothing is saved
 * until Save (ADR-0141). The wording's Translate follows the same rules for the
 * question, help text, and placeholder, and never touches a choice (ADR-0144).
 *
 * The choices are the question's own and are saved in place: editing them never
 * creates a new version, and a choice a reporter typed into a type-ahead is
 * marked here, where an administrator curates it (ADR-0095). Such a choice may
 * still be missing one language; it is the only one allowed to be.
 */

/** The question editor's view model: the draft's derived values, its Translate requests, and its handlers. */
export function useQuestionEditor({
	draft,
	conditionQuestions,
	choiceParentQuestions = [],
	onChange,
}: QuestionEditorProps): QuestionEditorViewModel {
	const { t, locale } = useLocale()
	const request = draft.request
	const takesOptions = OPTION_TYPES.includes(request.type)
	// Only a picker option is replaced; a type-ahead value is corrected in place
	// (ADR-0128, ADR-0129).
	const canReplace = request.type === "single_select" || request.type === "multi_select"
	// A statement or a group collects no answer, so it can be neither required,
	// private, a conditional child, nor a conditional parent (ADR-0076).
	const collectsNoAnswer = NO_ANSWER_TYPES.includes(request.type)
	const wording = wordingLabels(request.type)
	// A statement's description is often several paragraphs, so it gets room
	// for them; every other type's help text is one line.
	const helpTakesLines = request.type === "statement"
	const dependsOnParent = conditionQuestions.find((question) => question.id === request.dependsOnQuestionId)
	const dependsOnOptions =
		dependsOnParent?.type === "single_select"
			? sortChoices(dependsOnParent.options, locale, (option) => option.labelEn ?? option.labelFr ?? "")
			: []
	// Only a single-select's or type-ahead's choices may depend on another
	// question, and each then names the parent choices it is offered under (ADR-0151).
	const takesChoiceParent = request.type === "single_select" || request.type === "autocomplete"
	const choiceParent = takesChoiceParent
		? choiceParentQuestions.find((question) => question.id === request.choicesDependOnQuestionId)
		: undefined
	const parentChoices = choiceParent
		? sortChoices(choiceParent.options, locale, (option) => choiceLabel(option, locale))
		: []
	const parentGroup = parentChoices.map((choice) => ({ key: choice.id, label: choiceLabel(choice, locale) }))
	const unlinked = choiceParent
		? request.options.filter((option) => !parentChoices.some((choice) => option.parentChoiceIds?.includes(choice.id)))
		: []

	const [translating, setTranslating] = useState(false)
	const [translationError, setTranslationError] = useState<string | null>(null)
	const [direction, setDirection] = useState<TranslationDirection>(DEFAULT_TRANSLATION_DIRECTION)
	const [wordingDirection, setWordingDirection] = useState<TranslationDirection>(DEFAULT_TRANSLATION_DIRECTION)
	const [wordingBaseline, setWordingBaseline] = useState<Wording>(() => wordingOf(request))
	const [wordingTranslated, setWordingTranslated] = useState(false)
	const lastRowKey = useRef(0)
	const newRow = (option: Pick<OptionInput, "labelEn" | "labelFr">) => choiceRow(option, ++lastRowKey.current)
	const [rows, setRows] = useState<ChoiceRow[]>(() => request.options.map(newRow))
	const unavailableId = useId()
	const choicesHeadingId = useId()
	const unlinkedId = useId()
	const choicesRef = useRef<HTMLDivElement>(null)
	const focusNewChoice = useRef(false)

	// Read when a choice's translation comes back, to drop a result the
	// administrator has since made stale.
	const latest = useRef({ request, rows, direction, wordingDirection })
	useLayoutEffect(() => {
		latest.current = { request, rows, direction, wordingDirection }
	})

	// The rows follow the draft's options one for one. A draft replaced from
	// outside with a different number of choices starts its rows afresh.
	useEffect(() => {
		if (rows.length !== request.options.length) setRows(request.options.map(newRow))
	}, [rows.length, request.options])

	// A choice added from the panel header takes focus, so it is on screen
	// however long the list is.
	useEffect(() => {
		if (!focusNewChoice.current) return
		focusNewChoice.current = false
		const choices = choicesRef.current?.querySelectorAll<HTMLElement>('[data-testid="question-choice"]')
		choices?.[choices.length - 1]?.querySelector("input")?.focus()
	}, [request.options.length])

	const hasEnglish = request.labelEn.trim().length > 0
	const hasFrench = request.labelFr.trim().length > 0

	// A question is stored as one complete bilingual revision, so a half-written
	// one cannot be saved at all. Translate drafts the other side; the
	// administrator still edits and saves it deliberately (ADR-0062, ADR-0144). A
	// single-select condition additionally needs its required option named,
	// or the API rejects the save (ADR-0074).
	const colonEn = endsWithColon(request.labelEn)
	const colonFr = endsWithColon(request.labelFr)
	const canSave =
		hasEnglish &&
		hasFrench &&
		!colonEn &&
		!colonFr &&
		(dependsOnParent?.type !== "single_select" || request.dependsOnChoiceId !== null) &&
		unlinked.length === 0
	const wordingOffered = wordingFieldsToTranslate(wordingOf(request), wordingBaseline, wordingDirection).length > 0

	function translationFailure(cause: unknown) {
		return cause instanceof ApiError ? cause.detail : t("questions.translate.failed")
	}

	function update(changes: Partial<SaveQuestionRequest>) {
		onChange({ request: { ...request, ...changes } })
	}

	/**
	 * Translates the wording fields that need it, in the chosen direction, and
	 * replaces their targets with drafts. A field written in both languages
	 * whose source was not edited keeps its target. Choices are not touched;
	 * each is translated from the Choices panel (ADR-0144). A field is left
	 * alone if its source or target changed while the request was out, or if
	 * the translation came back empty, and the whole result is dropped if the
	 * direction flipped. Only the fields replaced are rebaselined.
	 */
	async function translateWording() {
		const asked = wordingDirection
		const { source, target } = wordingSides(asked)
		const sent = wordingOf(request)
		const fields = wordingFieldsToTranslate(sent, wordingBaseline, asked)
		if (fields.length === 0) return

		const { from, to } = translationLocales(asked)
		setTranslating(true)
		setTranslationError(null)

		try {
			// Every field in one request rather than one per field.
			const { texts } = await translate(
				fields.map((field) => sent[`${field}${source}`]),
				from,
				to,
			)
			if (latest.current.wordingDirection !== asked) return

			const stillAsSent = (now: Wording, field: WordingField) =>
				now[`${field}${source}`] === sent[`${field}${source}`] && now[`${field}${target}`] === sent[`${field}${target}`]
			const drafts = fields
				.map((field, index) => ({ field, drafted: (texts[index] ?? "").trim() ? texts[index] : "" }))
				.filter(({ field, drafted }) => drafted && stillAsSent(wordingOf(latest.current.request), field))
			if (drafts.length === 0) return

			onChange((current) => {
				const now = wordingOf(current.request)
				const changes: Partial<SaveQuestionRequest> = {}
				for (const { field, drafted } of drafts) {
					if (stillAsSent(now, field)) changes[`${field}${target}`] = drafted
				}
				return { request: { ...current.request, ...changes } }
			})

			// The replaced fields are clean again, in both directions, until
			// their source is edited.
			setWordingBaseline((baseline) => {
				const next = { ...baseline }
				for (const { field, drafted } of drafts) {
					next[`${field}${source}`] = sent[`${field}${source}`]
					next[`${field}${target}`] = drafted
				}
				return next
			})
			setWordingTranslated(true)
		} catch (cause) {
			setTranslationError(translationFailure(cause))
		} finally {
			setTranslating(false)
		}
	}

	function patchRow(key: number, changes: Partial<ChoiceRow>) {
		setRows((current) => current.map((row) => (row.key === key ? { ...row, ...changes } : row)))
	}

	/**
	 * Translates one choice in the chosen direction and replaces its other
	 * language with the result, as a draft. The result lands through a function
	 * of the current draft, on the row it was asked for, and is dropped if that
	 * row was removed, its source edited, or the direction flipped meanwhile.
	 */
	async function translateChoice(index: number) {
		const row = rows[index]
		const option = request.options[index]
		if (!row || !option) return

		const asked = direction
		const source = sourceOf(option, asked)
		const { from, to } = translationLocales(asked)
		patchRow(row.key, { pending: true, error: null })

		try {
			const { texts } = await translate([source], from, to)
			const drafted = texts[0] ?? ""
			const now = latest.current
			const at = now.rows.findIndex((candidate) => candidate.key === row.key)
			const current = now.request.options[at]
			if (at < 0 || !current || now.direction !== asked || sourceOf(current, asked) !== source) return

			onChange((draft) => ({
				request: {
					...draft.request,
					options: draft.request.options.map((candidate, position) =>
						position !== at || sourceOf(candidate, asked) !== source
							? candidate
							: asked === "toFrench"
								? { ...candidate, labelFr: drafted }
								: { ...candidate, labelEn: drafted },
					),
				},
			}))
			// Clean again until the source is edited.
			patchRow(
				row.key,
				asked === "toFrench" ? { baselineEn: source, baselineFr: drafted } : { baselineEn: drafted, baselineFr: source },
			)
		} catch (cause) {
			patchRow(row.key, { error: translationFailure(cause) })
		} finally {
			patchRow(row.key, { pending: false })
		}
	}

	function updateOption(
		index: number,
		changes: Partial<Pick<OptionInput, "labelEn" | "labelFr" | "replace" | "pin" | "parentChoiceIds">>,
	) {
		const options = request.options.map((option, current) => (current === index ? { ...option, ...changes } : option))
		update({ options })
	}

	function changeType(type: QuestionType) {
		// Choices only mean something for the types that take
		// them; carrying them across a retype would save choices
		// the question no longer offers.
		const clearedOptions = OPTION_TYPES.includes(type) ? {} : { options: [] }
		if (!OPTION_TYPES.includes(type)) setRows([])
		// A statement or a group collects no answer, so it cannot
		// be required, private, or conditional on anything
		// (ADR-0076).
		const clearedForNoAnswer = NO_ANSWER_TYPES.includes(type)
			? { isRequired: false, isPrivate: false, dependsOnQuestionId: null, dependsOnChoiceId: null }
			: {}
		// Only a single-select's or type-ahead's choices depend on
		// another question (ADR-0146).
		const clearedChoiceParent =
			type === "single_select" || type === "autocomplete" ? {} : { choicesDependOnQuestionId: null }
		// A retype takes the new type's translation default: only
		// free text can need translation at all (ADR-0112).
		update({
			type,
			...clearedOptions,
			...clearedForNoAnswer,
			...clearedChoiceParent,
			isTranslatable: translatableByDefault(type),
			// Only a date question may allow a future date (ADR-0138).
			allowFutureDates: false,
		})
	}

	function addChoice() {
		focusNewChoice.current = true
		setRows((current) => [...current, newRow({ labelEn: "", labelFr: "" })])
		update({ options: [...request.options, { code: null, labelEn: "", labelFr: "" }] })
	}

	function removeChoice(index: number) {
		setRows((current) => current.filter((_, position) => position !== index))
		update({ options: request.options.filter((_, current) => current !== index) })
	}

	function toggleParentChoice(index: number, id: string) {
		const current = request.options[index]?.parentChoiceIds ?? []
		updateOption(index, {
			parentChoiceIds: current.includes(id) ? current.filter((ticked) => ticked !== id) : [...current, id],
		})
	}

	const choiceItems: ChoiceItem[] = request.options.map((option, index) => {
		const row = rows[index]
		return {
			option,
			index,
			reactKey: row?.key ?? `new-${index}`,
			fieldKey: row?.key ?? index,
			// Only a reporter-added choice may be saved missing a
			// language; an administrator's own choice needs both.
			reporterAdded: option.addedByReporter === true,
			awaiting: !option.labelEn.trim()
				? t("questions.choice.awaitingEnglish")
				: !option.labelFr.trim()
					? t("questions.choice.awaitingFrench")
					: null,
			offered: row !== undefined && canTranslateChoice(option, row, direction),
			pending: row?.pending === true,
			error: row?.error ?? null,
			parentValues: (option.parentChoiceIds ?? []).filter((id) => parentChoices.some((choice) => choice.id === id)),
			invalid: unlinked.includes(option),
		}
	})

	return {
		request,
		choiceParentQuestions,
		questionTypes: QUESTION_TYPES,
		takesOptions,
		canReplace,
		collectsNoAnswer,
		translatableType: TRANSLATABLE_TYPES.includes(request.type),
		wording,
		helpTakesLines,
		dependsOnParent,
		dependsOnOptions,
		takesChoiceParent,
		choiceParent,
		parentGroup,
		unlinked,
		translating,
		translationError,
		direction,
		setDirection,
		wordingDirection,
		setWordingDirection,
		wordingTranslated,
		wordingOffered,
		colonEn,
		colonFr,
		canSave,
		choiceItems,
		unavailableId,
		choicesHeadingId,
		unlinkedId,
		choicesRef,
		update,
		updateOption,
		changeType,
		addChoice,
		removeChoice,
		toggleParentChoice,
		translateWording,
		translateChoice,
	}
}

export function QuestionEditor(props: QuestionEditorProps) {
	return <QuestionEditorView {...props} {...useQuestionEditor(props)} />
}
