import { act, renderHook, render, screen } from "@testing-library/react"
import { useState, type ReactNode } from "react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError, translate, type OptionView, type QuestionView } from "../api/adminQuestions"
import { LocaleContext } from "../i18n/LocaleProvider"
import type { Locale } from "../i18n/locales"
import { blankDraft, type QuestionDraft } from "./questionDraft"
import { QuestionEditor, useQuestionEditor } from "./QuestionEditor"
import type { QuestionEditorProps, QuestionEditorViewProps } from "./QuestionEditor.view"

vi.mock("../api/adminQuestions", async (importOriginal) => ({
	...(await importOriginal<typeof import("../api/adminQuestions")>()),
	translate: vi.fn(),
}))

const seen = vi.hoisted((): { props: unknown } => ({ props: null }))
vi.mock("./QuestionEditor.view", () => ({
	QuestionEditorView: (props: unknown) => {
		seen.props = props
		return <p>view</p>
	},
}))

const translateMock = vi.mocked(translate)

function wrapperFor(locale: Locale) {
	return ({ children }: { children: ReactNode }) => (
		<LocaleContext.Provider value={{ locale, setLocale: () => {}, t: (key) => key }}>{children}</LocaleContext.Provider>
	)
}

function option(id: string, over: Partial<OptionView> = {}): OptionView {
	return {
		id,
		code: id,
		labelEn: id,
		labelFr: id,
		addedByReporter: false,
		needsTranslation: false,
		reporterLocale: null,
		pin: "none",
		parentChoiceIds: [],
		...over,
	}
}

function question(id: string, over: Partial<QuestionView> = {}): QuestionView {
	return {
		id,
		key: id,
		revisionId: "r",
		revisionNumber: 1,
		type: "single_select",
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
		labelEn: id,
		labelFr: id,
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

function draftWith(changes: Partial<QuestionDraft["request"]>): QuestionDraft {
	return { request: { ...blankDraft().request, ...changes } }
}

/** The editor's view model with the draft held in state, as the page holds it. */
function renderEditor(
	initial: QuestionDraft,
	over: Partial<QuestionEditorProps> = {},
	locale: Locale = "en-CA",
) {
	return renderHook(
		() => {
			const [draft, setDraft] = useState(initial)
			const model = useQuestionEditor({
				draft,
				conditionQuestions: [],
				groupQuestions: [],
				isEditing: false,
				hasBeenAnswered: false,
				translationAvailable: true,
				onChange: (change) => setDraft((current) => (typeof change === "function" ? change(current) : change)),
				onCancel: () => {},
				onSave: () => {},
				...over,
			})
			return { ...model, draft }
		},
		{ wrapper: wrapperFor(locale) },
	)
}

beforeEach(() => {
	translateMock.mockReset()
})

describe("useQuestionEditor derived values", () => {
	it("treats a short-text question as taking no choices, and a statement as collecting no answer", () => {
		const { result, rerender } = renderEditor(blankDraft())

		expect(result.current).toMatchObject({
			takesOptions: false,
			canReplace: false,
			collectsNoAnswer: false,
			translatableType: true,
			helpTakesLines: false,
			takesChoiceParent: false,
			choiceParent: undefined,
			unlinked: [],
			choiceParentQuestions: [],
			wording: { labelEn: "labelEn" },
		})
		act(() => result.current.changeType("statement"))
		rerender()

		expect(result.current).toMatchObject({ collectsNoAnswer: true, helpTakesLines: true, wording: { labelEn: "titleEn" } })
	})

	it("treats a multi-select as replaceable and a type-ahead as taking a choice parent", () => {
		const multi = renderEditor(draftWith({ type: "multi_select" })).result.current
		const ahead = renderEditor(draftWith({ type: "autocomplete" })).result.current

		expect(multi).toMatchObject({ takesOptions: true, canReplace: true, takesChoiceParent: false })
		expect(ahead).toMatchObject({ takesOptions: true, canReplace: false, takesChoiceParent: true })
	})

	it("lists a single-select condition's choices in the reader's order, or none for another parent", () => {
		const parent = question("p", {
			options: [option("b", { labelEn: "B" }), option("a", { labelEn: null, labelFr: "A" }), option("z", { labelEn: null, labelFr: null })],
		})
		const yesNo = question("y", { type: "yes_no" })

		const single = renderEditor(draftWith({ dependsOnQuestionId: "p" }), { conditionQuestions: [parent] }).result.current
		const other = renderEditor(draftWith({ dependsOnQuestionId: "y" }), { conditionQuestions: [yesNo] }).result.current

		expect(single.dependsOnParent).toBe(parent)
		expect(single.dependsOnOptions.map((choice) => choice.id)).toEqual(["z", "a", "b"])
		expect(other.dependsOnOptions).toEqual([])
		expect(single.canSave).toBe(false)
	})

	it("reads Save as blocked until both languages are present, colon-free, and every condition is named", () => {
		const base = { labelEn: "En", labelFr: "Fr" }

		expect(renderEditor(draftWith(base)).result.current.canSave).toBe(true)
		expect(renderEditor(draftWith({ ...base, labelEn: " " })).result.current.canSave).toBe(false)
		expect(renderEditor(draftWith({ ...base, labelFr: "" })).result.current.canSave).toBe(false)
		const colons = renderEditor(draftWith({ labelEn: "En:", labelFr: "Fr :" })).result.current
		expect(colons).toMatchObject({ colonEn: true, colonFr: true, canSave: false })

		const parent = question("p", { options: [option("a")] })
		const needsOption = renderEditor(draftWith({ ...base, dependsOnQuestionId: "p" }), { conditionQuestions: [parent] })
		expect(needsOption.result.current.canSave).toBe(false)
		const named = renderEditor(draftWith({ ...base, dependsOnQuestionId: "p", dependsOnChoiceId: "a" }), {
			conditionQuestions: [parent],
		})
		expect(named.result.current.canSave).toBe(true)
	})

	it("finds the choice parent among the offered questions and flags choices linked to none of its live choices", () => {
		const parent = question("p", { options: [option("bc", { labelEn: "BC", labelFr: "CB" }), option("ab", { labelEn: "AB", labelFr: "AB" })] })
		const draft = draftWith({
			type: "single_select",
			choicesDependOnQuestionId: "p",
			options: [
				{ code: "x", labelEn: "Linked", labelFr: "Lié", parentChoiceIds: ["bc", "gone"] },
				{ code: "y", labelEn: "Orphan", labelFr: "Orphelin" },
			],
		})

		const en = renderEditor(draft, { choiceParentQuestions: [parent] }, "en-CA").result.current
		const fr = renderEditor(draft, { choiceParentQuestions: [parent] }, "fr-CA").result.current

		expect(en.choiceParent).toBe(parent)
		expect(en.parentGroup).toEqual([
			{ key: "ab", label: "AB" },
			{ key: "bc", label: "BC" },
		])
		expect(fr.parentGroup.map((choice) => choice.label)).toEqual(["AB", "CB"])
		expect(en.unlinked.map((choice) => choice.code)).toEqual(["y"])
		expect(en.canSave).toBe(false)
		expect(en.choiceItems.map((item) => [item.parentValues, item.invalid])).toEqual([
			[["bc"], false],
			[[], true],
		])
	})

	it("finds no choice parent when the draft names none the editor offers", () => {
		const result = renderEditor(draftWith({ type: "single_select", choicesDependOnQuestionId: "zzz" })).result.current

		expect(result.choiceParent).toBeUndefined()
		expect(result.unlinked).toEqual([])
	})
})

describe("useQuestionEditor choice rows", () => {
	it("describes each choice: its keys, who added it, what it awaits, and whether Translate is offered", () => {
		const { result } = renderEditor(
			draftWith({
				type: "autocomplete",
				options: [
					{ code: "a", labelEn: "", labelFr: "Un", addedByReporter: true },
					{ code: "b", labelEn: "Two", labelFr: "", addedByReporter: true },
					{ code: "c", labelEn: "Three", labelFr: "Trois" },
				],
			}),
		)

		expect(result.current.choiceItems.map((item) => [item.reactKey, item.fieldKey, item.reporterAdded, item.awaiting, item.offered, item.pending, item.error])).toEqual([
			[1, 1, true, "questions.choice.awaitingEnglish", false, false, null],
			[2, 2, true, "questions.choice.awaitingFrench", true, false, null],
			[3, 3, false, null, false, false, null],
		])
	})

	it("keys a choice by position until its row exists", () => {
		const { result } = renderEditor(draftWith({ type: "single_select" }))
		const request = result.current.request

		act(() => result.current.update({ options: [...request.options, { code: null, labelEn: "", labelFr: "" }] }))

		expect(result.current.choiceItems).toHaveLength(1)
		expect(result.current.draft.request.options).toHaveLength(1)
	})

	it("starts the rows afresh when the draft is replaced with a different number of choices", () => {
		const { result } = renderEditor(draftWith({ type: "single_select" }))

		act(() => result.current.update({ options: [{ code: null, labelEn: "A", labelFr: "A" }, { code: null, labelEn: "B", labelFr: "B" }] }))

		expect(result.current.choiceItems.map((item) => item.reactKey)).toEqual([1, 2])
	})

	it("adds a choice and focuses its first field, and removes a choice and its row", () => {
		const { result } = renderEditor(draftWith({ type: "single_select", options: [{ code: "a", labelEn: "A", labelFr: "A" }] }))
		const container = document.createElement("div")
		for (let index = 0; index < 2; index++) {
			const choice = document.createElement("div")
			choice.dataset.testid = "question-choice"
			choice.append(document.createElement("input"))
			container.append(choice)
		}
		document.body.append(container)
		;(result.current.choicesRef as { current: HTMLDivElement | null }).current = container

		act(() => result.current.addChoice())

		expect(result.current.draft.request.options).toHaveLength(2)
		expect(document.activeElement).toBe(container.querySelectorAll("input")[1])

		act(() => result.current.removeChoice(0))

		expect(result.current.draft.request.options.map((choice) => choice.labelEn)).toEqual([""])
		expect(result.current.choiceItems).toHaveLength(1)
		container.remove()
	})

	it("does not fail to focus when the panel is missing or has no choice with a field", () => {
		const bare = renderEditor(draftWith({ type: "single_select" }))
		act(() => bare.result.current.addChoice())

		const empty = renderEditor(draftWith({ type: "single_select" }))
		const container = document.createElement("div")
		const choice = document.createElement("div")
		choice.dataset.testid = "question-choice"
		container.append(choice)
		;(empty.result.current.choicesRef as { current: HTMLDivElement | null }).current = container
		act(() => empty.result.current.addChoice())

		expect(bare.result.current.draft.request.options).toHaveLength(1)
		expect(empty.result.current.draft.request.options).toHaveLength(1)
	})

	it("ticks and unticks a parent choice, starting from none when the choice has no links", () => {
		const { result } = renderEditor(
			draftWith({ type: "single_select", options: [{ code: "a", labelEn: "A", labelFr: "A" }] }),
		)

		act(() => result.current.toggleParentChoice(0, "x"))
		expect(result.current.draft.request.options[0]?.parentChoiceIds).toEqual(["x"])
		act(() => result.current.toggleParentChoice(0, "y"))
		expect(result.current.draft.request.options[0]?.parentChoiceIds).toEqual(["x", "y"])
		act(() => result.current.toggleParentChoice(0, "x"))
		expect(result.current.draft.request.options[0]?.parentChoiceIds).toEqual(["y"])
	})

	it("treats toggling a choice that does not exist as ticking from none", () => {
		const { result } = renderEditor(draftWith({ type: "single_select" }))

		act(() => result.current.toggleParentChoice(4, "x"))

		expect(result.current.draft.request.options).toEqual([])
	})

	it("changes one option's fields", () => {
		const { result } = renderEditor(
			draftWith({ type: "single_select", options: [{ code: "a", labelEn: "A", labelFr: "A" }, { code: "b", labelEn: "B", labelFr: "B" }] }),
		)

		act(() => result.current.updateOption(1, { labelEn: "B2" }))

		expect(result.current.draft.request.options.map((choice) => choice.labelEn)).toEqual(["A", "B2"])
	})
})

describe("useQuestionEditor changeType", () => {
	it("keeps choices across a retype to another choice type and takes the new type's defaults", () => {
		const { result } = renderEditor(
			draftWith({ type: "single_select", allowFutureDates: true, options: [{ code: "a", labelEn: "A", labelFr: "A" }], choicesDependOnQuestionId: "p" }),
		)

		act(() => result.current.changeType("multi_select"))

		expect(result.current.draft.request).toMatchObject({
			type: "multi_select",
			isTranslatable: false,
			allowFutureDates: false,
			choicesDependOnQuestionId: null,
		})
		expect(result.current.draft.request.options).toHaveLength(1)
	})

	it("keeps the choice parent for a type-ahead", () => {
		const { result } = renderEditor(draftWith({ type: "single_select", choicesDependOnQuestionId: "p" }))

		act(() => result.current.changeType("autocomplete"))

		expect(result.current.draft.request.choicesDependOnQuestionId).toBe("p")
	})

	it("clears choices and their rows for a type that takes none", () => {
		const { result } = renderEditor(
			draftWith({ type: "single_select", options: [{ code: "a", labelEn: "A", labelFr: "A" }] }),
		)

		act(() => result.current.changeType("long_text"))

		expect(result.current.draft.request).toMatchObject({ type: "long_text", options: [], isTranslatable: true })
		expect(result.current.choiceItems).toEqual([])
	})

	it("clears required, private and conditions for a type that collects no answer", () => {
		const { result } = renderEditor(
			draftWith({ isRequired: true, isPrivate: true, dependsOnQuestionId: "q", dependsOnChoiceId: "c" }),
		)

		act(() => result.current.changeType("group"))

		expect(result.current.draft.request).toMatchObject({
			type: "group",
			isRequired: false,
			isPrivate: false,
			dependsOnQuestionId: null,
			dependsOnChoiceId: null,
		})
	})
})

describe("useQuestionEditor translateWording", () => {
	const needsFrench = () => draftWith({ labelEn: "Hello", labelFr: "", helpTextEn: "Help", helpTextFr: "Aide" })

	it("does nothing when no field needs translating", async () => {
		const { result } = renderEditor(draftWith({ labelEn: "Hello", labelFr: "Bonjour", helpTextEn: null, helpTextFr: null }))

		await act(() => result.current.translateWording())

		expect(translateMock).not.toHaveBeenCalled()
		expect(result.current.wordingOffered).toBe(false)
	})

	it("drafts the target of each field that needs it and marks them clean", async () => {
		translateMock.mockResolvedValue({ texts: ["Bonjour"] })
		const { result } = renderEditor(needsFrench())
		expect(result.current.wordingOffered).toBe(true)

		await act(() => result.current.translateWording())

		expect(translateMock).toHaveBeenCalledWith(["Hello"], "en-CA", "fr-CA")
		expect(result.current.draft.request).toMatchObject({ labelFr: "Bonjour", helpTextFr: "Aide" })
		expect(result.current.wordingTranslated).toBe(true)
		expect(result.current.wordingOffered).toBe(false)
		expect(result.current.translating).toBe(false)
	})

	it("translates French to English when the direction is flipped", async () => {
		translateMock.mockResolvedValue({ texts: ["Hello"] })
		const { result } = renderEditor(draftWith({ labelEn: "", labelFr: "Bonjour" }))
		act(() => result.current.setWordingDirection("toEnglish"))

		await act(() => result.current.translateWording())

		expect(translateMock).toHaveBeenCalledWith(["Bonjour"], "fr-CA", "en-CA")
		expect(result.current.draft.request.labelEn).toBe("Hello")
	})

	it("shows the working state while the request is out", async () => {
		let finish: (value: { texts: string[] }) => void = () => {}
		translateMock.mockReturnValue(new Promise((resolve) => (finish = resolve)))
		const { result } = renderEditor(needsFrench())

		let pending: void | Promise<void>
		act(() => {
			pending = result.current.translateWording()
		})
		expect(result.current.translating).toBe(true)
		await act(async () => {
			finish({ texts: ["Bonjour"] })
			await pending
		})

		expect(result.current.translating).toBe(false)
	})

	it("drops the whole result if the direction flipped while it was out", async () => {
		let finish: (value: { texts: string[] }) => void = () => {}
		translateMock.mockReturnValue(new Promise((resolve) => (finish = resolve)))
		const { result } = renderEditor(needsFrench())
		let pending: void | Promise<void>
		act(() => {
			pending = result.current.translateWording()
		})
		act(() => result.current.setWordingDirection("toEnglish"))

		await act(async () => {
			finish({ texts: ["Bonjour"] })
			await pending
		})

		expect(result.current.draft.request.labelFr).toBe("")
		expect(result.current.wordingTranslated).toBe(false)
	})

	it("leaves a field alone if its source was edited while the request was out", async () => {
		let finish: (value: { texts: string[] }) => void = () => {}
		translateMock.mockReturnValue(new Promise((resolve) => (finish = resolve)))
		const { result } = renderEditor(needsFrench())
		let pending: void | Promise<void>
		act(() => {
			pending = result.current.translateWording()
		})
		act(() => result.current.update({ labelEn: "Edited" }))

		await act(async () => {
			finish({ texts: ["Bonjour"] })
			await pending
		})

		expect(result.current.draft.request.labelFr).toBe("")
		expect(result.current.wordingTranslated).toBe(false)
	})

	it("ignores an empty translation", async () => {
		translateMock.mockResolvedValue({ texts: ["  "] })
		const { result } = renderEditor(needsFrench())

		await act(() => result.current.translateWording())

		expect(result.current.draft.request.labelFr).toBe("")
		expect(result.current.wordingTranslated).toBe(false)
	})

	it("ignores a translation the service returned fewer texts for", async () => {
		translateMock.mockResolvedValue({ texts: [] })
		const { result } = renderEditor(needsFrench())

		await act(() => result.current.translateWording())

		expect(result.current.draft.request.labelFr).toBe("")
	})

	it("writes only the fields still as sent when the result lands on a draft that moved on", async () => {
		translateMock.mockResolvedValue({ texts: ["Bonjour", "Aide"] })
		let landing: (current: QuestionDraft) => QuestionDraft = (current) => current
		const initial = draftWith({ labelEn: "Hello", labelFr: "", helpTextEn: "Help", helpTextFr: "" })
		const { result } = renderEditor(initial, {
			onChange: (change) => {
				if (typeof change === "function") landing = change
			},
		})

		await act(() => result.current.translateWording())
		const moved = landing({ request: { ...initial.request, helpTextEn: "Edited" } })

		expect(moved.request).toMatchObject({ labelFr: "Bonjour", helpTextFr: "" })
	})

	it("reports the API's detail on failure, or a generic message for anything else", async () => {
		translateMock.mockRejectedValueOnce(new ApiError(500, "Provider is down"))
		const { result } = renderEditor(needsFrench())

		await act(() => result.current.translateWording())
		expect(result.current.translationError).toBe("Provider is down")

		translateMock.mockRejectedValueOnce(new Error("boom"))
		await act(() => result.current.translateWording())
		expect(result.current.translationError).toBe("questions.translate.failed")
		expect(result.current.translating).toBe(false)
	})
})

describe("useQuestionEditor translateChoice", () => {
	const choices = () =>
		draftWith({
			type: "single_select",
			options: [
				{ code: "a", labelEn: "Apple", labelFr: "" },
				{ code: "b", labelEn: "", labelFr: "Banane" },
			],
		})

	it("does nothing for a choice that does not exist", async () => {
		const { result } = renderEditor(choices())

		await act(() => result.current.translateChoice(9))

		expect(translateMock).not.toHaveBeenCalled()
	})

	it("replaces the other language with a draft and marks the row clean", async () => {
		translateMock.mockResolvedValue({ texts: ["Pomme"] })
		const { result } = renderEditor(choices())

		await act(() => result.current.translateChoice(0))

		expect(translateMock).toHaveBeenCalledWith(["Apple"], "en-CA", "fr-CA")
		expect(result.current.draft.request.options[0]?.labelFr).toBe("Pomme")
		expect(result.current.choiceItems[0]).toMatchObject({ offered: false, pending: false, error: null })
	})

	it("translates French to English when the direction is flipped", async () => {
		translateMock.mockResolvedValue({ texts: ["Banana"] })
		const { result } = renderEditor(choices())
		act(() => result.current.setDirection("toEnglish"))

		await act(() => result.current.translateChoice(1))

		expect(translateMock).toHaveBeenCalledWith(["Banane"], "fr-CA", "en-CA")
		expect(result.current.draft.request.options[1]?.labelEn).toBe("Banana")
	})

	it("marks the row pending while the request is out", async () => {
		let finish: (value: { texts: string[] }) => void = () => {}
		translateMock.mockReturnValue(new Promise((resolve) => (finish = resolve)))
		const { result } = renderEditor(choices())
		let pending: void | Promise<void>
		act(() => {
			pending = result.current.translateChoice(0)
		})
		expect(result.current.choiceItems[0]?.pending).toBe(true)

		await act(async () => {
			finish({ texts: ["Pomme"] })
			await pending
		})

		expect(result.current.choiceItems[0]?.pending).toBe(false)
	})

	it("treats a missing translation as an empty draft", async () => {
		translateMock.mockResolvedValue({ texts: [] })
		const { result } = renderEditor(choices())

		await act(() => result.current.translateChoice(0))

		expect(result.current.draft.request.options[0]?.labelFr).toBe("")
	})

	it("drops the result if the choice's source was edited while the request was out", async () => {
		let finish: (value: { texts: string[] }) => void = () => {}
		translateMock.mockReturnValue(new Promise((resolve) => (finish = resolve)))
		const { result } = renderEditor(choices())
		let pending: void | Promise<void>
		act(() => {
			pending = result.current.translateChoice(0)
		})
		act(() => result.current.updateOption(0, { labelEn: "Pear" }))

		await act(async () => {
			finish({ texts: ["Pomme"] })
			await pending
		})

		expect(result.current.draft.request.options[0]).toMatchObject({ labelEn: "Pear", labelFr: "" })
	})

	it("drops the result if the choice was removed, or the direction flipped", async () => {
		let finish: (value: { texts: string[] }) => void = () => {}
		translateMock.mockReturnValue(new Promise((resolve) => (finish = resolve)))
		const { result } = renderEditor(choices())
		let pending: void | Promise<void>
		act(() => {
			pending = result.current.translateChoice(0)
		})
		act(() => result.current.removeChoice(0))
		await act(async () => {
			finish({ texts: ["Pomme"] })
			await pending
		})
		expect(result.current.draft.request.options.map((choice) => choice.labelEn)).toEqual([""])

		translateMock.mockReturnValue(new Promise((resolve) => (finish = resolve)))
		act(() => {
			pending = result.current.translateChoice(0)
		})
		act(() => result.current.setDirection("toEnglish"))
		await act(async () => {
			finish({ texts: ["Banana"] })
			await pending
		})
		expect(result.current.draft.request.options[0]?.labelEn).toBe("")
	})

	it("leaves another choice alone when the result lands on a draft whose choices moved", async () => {
		let finish: (value: { texts: string[] }) => void = () => {}
		translateMock.mockReturnValue(new Promise((resolve) => (finish = resolve)))
		const { result } = renderEditor(choices())
		let pending: void | Promise<void>
		act(() => {
			pending = result.current.translateChoice(0)
		})

		await act(async () => {
			finish({ texts: ["Pomme"] })
			await pending
		})

		expect(result.current.draft.request.options[1]).toMatchObject({ labelEn: "", labelFr: "Banane" })
	})

	it("shows the failure on the row it was asked for", async () => {
		translateMock.mockRejectedValueOnce(new ApiError(500, "Provider is down"))
		const { result } = renderEditor(choices())

		await act(() => result.current.translateChoice(0))

		expect(result.current.choiceItems[0]).toMatchObject({ error: "Provider is down", pending: false })

		translateMock.mockRejectedValueOnce("boom")
		await act(() => result.current.translateChoice(0))
		expect(result.current.choiceItems[0]?.error).toBe("questions.translate.failed")
	})
})

describe("QuestionEditor", () => {
	it("renders its view with the props it was given and the view model", () => {
		const props: QuestionEditorProps = {
			draft: blankDraft(),
			conditionQuestions: [],
			groupQuestions: [],
			isEditing: true,
			hasBeenAnswered: true,
			translationAvailable: true,
			onChange: () => {},
			onCancel: () => {},
			onSave: () => {},
		}

		render(<QuestionEditor {...props} />, { wrapper: wrapperFor("en-CA") })

		expect(screen.getByText("view")).toBeTruthy()
		expect(seen.props).toMatchObject({ isEditing: true, hasBeenAnswered: true, canSave: false, choiceParentQuestions: [] })
		expect((seen.props as QuestionEditorViewProps).draft).toBe(props.draft)
	})
})
