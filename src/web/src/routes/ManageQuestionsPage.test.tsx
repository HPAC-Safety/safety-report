import { act, render, renderHook, screen, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import {
	ApiError,
	createQuestion,
	deleteQuestion,
	listQuestions,
	reorderQuestions,
	reviseQuestion,
	translationAvailable,
	type OptionView,
	type QuestionView,
} from "../api/adminQuestions"
import { exportTypeform, type ImportedQuestionDraftView } from "../api/adminTypeformImport"
import { LocaleContext } from "../i18n/LocaleProvider"
import { blankDraft } from "../components/questionDraft"
import { useUnsavedChangesGuard } from "../hooks/useUnsavedChangesGuard"
import { ManageQuestionsPage, useManageQuestionsPage } from "./ManageQuestionsPage"

vi.mock("../api/adminQuestions", async (importOriginal) => ({
	...(await importOriginal<typeof import("../api/adminQuestions")>()),
	listQuestions: vi.fn(),
	createQuestion: vi.fn(),
	reviseQuestion: vi.fn(),
	deleteQuestion: vi.fn(),
	reorderQuestions: vi.fn(),
	translationAvailable: vi.fn(),
}))
vi.mock("../api/adminTypeformImport", () => ({ exportTypeform: vi.fn() }))
vi.mock("../hooks/useUnsavedChangesGuard", () => ({ useUnsavedChangesGuard: vi.fn() }))

const seen = vi.hoisted((): { props: unknown } => ({ props: null }))
vi.mock("./ManageQuestionsPage.view", () => ({
	ManageQuestionsPageView: (props: unknown) => {
		seen.props = props
		return <p>page view</p>
	},
}))

const list = vi.mocked(listQuestions)
const create = vi.mocked(createQuestion)
const revise = vi.mocked(reviseQuestion)
const remove = vi.mocked(deleteQuestion)
const reorder = vi.mocked(reorderQuestions)
const available = vi.mocked(translationAvailable)
const exporter = vi.mocked(exportTypeform)
const guard = vi.mocked(useUnsavedChangesGuard)

function wrapper({ children }: { children: ReactNode }) {
	return <LocaleContext.Provider value={{ locale: "fr-CA", setLocale: () => {}, t: (key) => key }}>{children}</LocaleContext.Provider>
}

function option(id: string): OptionView {
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
	}
}

function question(id: string, over: Partial<QuestionView> = {}): QuestionView {
	return {
		id,
		key: `key-${id}`,
		revisionId: "r",
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
		labelEn: id,
		labelFr: id,
		helpTextEn: null,
		helpTextFr: null,
		placeholderEn: null,
		placeholderFr: null,
		options: [option("o")],
		reporterChoicesAwaitingReview: 0,
		hasBeenAnswered: false,
		...over,
	}
}

function imported(key: string): ImportedQuestionDraftView {
	return {
		key,
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
	}
}

/** The page's view model, loaded. */
async function load(questions: QuestionView[] = []) {
	list.mockResolvedValue(questions)
	const rendered = renderHook(() => useManageQuestionsPage(), { wrapper })
	await waitFor(() => expect(rendered.result.current.loading).toBe(false))
	return rendered
}

beforeEach(() => {
	vi.resetAllMocks()
	list.mockResolvedValue([])
	available.mockResolvedValue({ available: true })
})

describe("useManageQuestionsPage loading", () => {
	it("is loading until the questions and the translation availability arrive", async () => {
		const { result } = renderHook(() => useManageQuestionsPage(), { wrapper })
		expect(result.current.loading).toBe(true)

		await waitFor(() => expect(result.current.loading).toBe(false))
		expect(result.current.questions).toEqual([])
		expect(result.current.error).toBeNull()
	})

	it("offers translation only when the server has a credential", async () => {
		const { result } = await load([question("a")])
		act(() => result.current.editQuestion(question("a")))
		expect(result.current.editor?.translationAvailable).toBe(true)

		available.mockResolvedValue({ available: false })
		const off = await load()
		act(() => off.result.current.startNew())
		expect(off.result.current.editor?.translationAvailable).toBe(false)
	})

	it("treats a failed availability check as unavailable", async () => {
		available.mockRejectedValue(new Error("down"))
		const { result } = await load()
		act(() => result.current.startNew())

		expect(result.current.error).toBeNull()
		expect(result.current.editor?.translationAvailable).toBe(false)
	})

	it("shows the API's detail when loading fails, or a generic message", async () => {
		list.mockRejectedValue(new ApiError(403, "Forbidden"))
		const refused = renderHook(() => useManageQuestionsPage(), { wrapper })
		await waitFor(() => expect(refused.result.current.error).toBe("Forbidden"))
		expect(refused.result.current.loading).toBe(false)

		list.mockRejectedValue(new Error("boom"))
		const broken = renderHook(() => useManageQuestionsPage(), { wrapper })
		await waitFor(() => expect(broken.result.current.error).toBe("questions.error.unexpected"))
	})
})

describe("useManageQuestionsPage editing", () => {
	it("opens a blank draft, closing the import, and cancelling closes it", async () => {
		const { result } = await load()
		act(() => result.current.startImport())
		expect(result.current.importing).toBe(true)

		act(() => result.current.startNew())

		expect(result.current.importing).toBe(false)
		expect(result.current.editing).toBeNull()
		expect(result.current.editor?.draft).toEqual(blankDraft())
		expect(result.current.editor?.isEditing).toBe(false)

		act(() => result.current.editor?.onCancel())
		expect(result.current.editor).toBeNull()
	})

	it("opens the import dialog, closing any editor, and closes it again", async () => {
		const { result } = await load([question("a")])
		act(() => result.current.editQuestion(question("a")))

		act(() => result.current.startImport())
		expect(result.current).toMatchObject({ importing: true, editor: null, editing: null })

		act(() => result.current.closeImport())
		expect(result.current.importing).toBe(false)
	})

	it("opens an existing question in the reader's language and says when it has been answered", async () => {
		const answered = question("a", { hasBeenAnswered: true, labelEn: "Zed", options: [option("o")] })
		const { result } = await load([answered, question("b")])

		act(() => result.current.editQuestion(answered))
		expect(result.current.editing).toBe("a")
		expect(result.current.editor).toMatchObject({ isEditing: true, hasBeenAnswered: true })
		expect(result.current.editor?.draft.request.labelEn).toBe("Zed")

		act(() => result.current.editQuestion(question("b")))
		expect(result.current.editor?.hasBeenAnswered).toBe(false)
	})

	it("applies a change to the draft, as a value or as a function of the current draft", async () => {
		const { result } = await load()
		act(() => result.current.startNew())
		const editor = result.current.editor

		act(() => editor?.onChange({ request: { ...blankDraft().request, labelEn: "Typed" } }))
		expect(result.current.editor?.draft.request.labelEn).toBe("Typed")

		act(() => editor?.onChange((current) => ({ request: { ...current.request, labelFr: "Tapé" } })))
		expect(result.current.editor?.draft.request).toMatchObject({ labelEn: "Typed", labelFr: "Tapé" })
	})

	it("ignores a late change once the editor has closed", async () => {
		const { result } = await load()
		act(() => result.current.startNew())
		const editor = result.current.editor
		act(() => editor?.onCancel())

		act(() => editor?.onChange((current) => ({ request: { ...current.request, labelEn: "Late" } })))

		expect(result.current.editor).toBeNull()
	})

	it("guards unsaved changes only once the draft differs from how it opened", async () => {
		const { result } = await load()
		expect(guard).toHaveBeenLastCalledWith(false)

		act(() => result.current.startNew())
		expect(guard).toHaveBeenLastCalledWith(false)

		act(() => result.current.editor?.onChange({ request: { ...blankDraft().request, labelEn: "Typed" } }))
		expect(guard).toHaveBeenLastCalledWith(true)
	})
})

describe("useManageQuestionsPage importing", () => {
	it("reviews an imported draft whose key matches a live question as an edit of it", async () => {
		const { result } = await load([question("a")])
		act(() => result.current.startImport())

		act(() => result.current.reviewImported(imported("key-a")))

		expect(result.current).toMatchObject({ editing: "a", importing: false })
		expect(result.current.editor?.draft.request.key).toBe("key-a")
	})

	it("reviews an imported draft with a new key as a new question", async () => {
		const { result } = await load([question("a")])

		act(() => result.current.reviewImported(imported("brand-new")))

		expect(result.current.editing).toBeNull()
		expect(result.current.editor?.draft.request.key).toBe("brand-new")
	})
})

describe("useManageQuestionsPage saving", () => {
	it("creates a new question, closes the editor and reloads", async () => {
		const { result } = await load()
		act(() => result.current.startNew())
		const draft = result.current.editor!.draft
		list.mockResolvedValue([question("made")])

		await act(() => result.current.editor!.onSave(draft))

		expect(create).toHaveBeenCalledWith(draft.request)
		expect(revise).not.toHaveBeenCalled()
		expect(result.current.editor).toBeNull()
		expect(result.current.questions.map((q) => q.id)).toEqual(["made"])
	})

	it("revises the question being edited", async () => {
		const { result } = await load([question("a")])
		act(() => result.current.editQuestion(question("a")))
		const draft = result.current.editor!.draft

		await act(() => result.current.editor!.onSave(draft))

		expect(revise).toHaveBeenCalledWith("a", draft.request)
		expect(result.current.editing).toBeNull()
	})

	it("keeps the editor open and shows the refusal when saving fails", async () => {
		create.mockRejectedValue(new ApiError(400, "Label required"))
		const { result } = await load()
		act(() => result.current.startNew())

		await act(() => result.current.editor!.onSave(result.current.editor!.draft))

		expect(result.current.error).toBe("Label required")
		expect(result.current.editor).not.toBeNull()
	})
})

describe("useManageQuestionsPage removing", () => {
	it("deletes the question and reloads", async () => {
		const { result } = await load([question("a")])
		list.mockResolvedValue([])

		await act(() => result.current.remove(question("a")))

		expect(remove).toHaveBeenCalledWith("a")
		expect(result.current.questions).toEqual([])
	})

	it("shows the refusal when deleting fails", async () => {
		remove.mockRejectedValue(new Error("boom"))
		const { result } = await load([question("a")])

		await act(() => result.current.remove(question("a")))

		expect(result.current.error).toBe("questions.error.unexpected")
	})
})

describe("useManageQuestionsPage exporting", () => {
	it("downloads the exported bank as question-bank.zip", async () => {
		const blob = new Blob(["zip"])
		exporter.mockResolvedValue(blob)
		const createUrl = vi.fn(() => "blob:bank")
		const revokeUrl = vi.fn()
		vi.stubGlobal("URL", { createObjectURL: createUrl, revokeObjectURL: revokeUrl })
		const clicked: HTMLAnchorElement[] = []
		const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function (this: HTMLAnchorElement) {
			clicked.push(this)
		})
		const { result } = await load()

		await act(() => result.current.exportBank())

		expect(createUrl).toHaveBeenCalledWith(blob)
		expect(clicked[0]).toMatchObject({ download: "question-bank.zip" })
		expect(clicked[0]?.href).toBe("blob:bank")
		expect(revokeUrl).toHaveBeenCalledWith("blob:bank")
		expect(result.current.exporting).toBe(false)
		click.mockRestore()
		vi.unstubAllGlobals()
	})

	it("is exporting while the request is out, and shows the refusal when it fails", async () => {
		let fail: (cause: unknown) => void = () => {}
		exporter.mockReturnValue(new Promise((_, reject) => (fail = reject)))
		const { result } = await load()
		let pending: void | Promise<void>
		act(() => {
			pending = result.current.exportBank()
		})
		expect(result.current.exporting).toBe(true)

		await act(async () => {
			fail(new ApiError(500, "Export failed"))
			await pending
		})

		expect(result.current.exporting).toBe(false)
		expect(result.current.error).toBe("Export failed")
	})
})

describe("useManageQuestionsPage reordering", () => {
	it("shows the order the server returned", async () => {
		const { result } = await load([question("a"), question("b")])
		reorder.mockResolvedValue([question("b"), question("a")])

		await act(() => result.current.reorder(["b", "a"]))

		expect(reorder).toHaveBeenCalledWith(["b", "a"])
		expect(result.current.questions.map((q) => q.id)).toEqual(["b", "a"])
	})

	it("reloads and keeps the refusal on screen when the server refuses", async () => {
		const { result } = await load([question("a"), question("b")])
		reorder.mockRejectedValue(new ApiError(409, "A parent must stay above"))
		list.mockResolvedValue([question("a"), question("b")])

		await act(() => result.current.reorder(["b", "a"]))

		expect(list).toHaveBeenCalledTimes(2)
		expect(result.current.error).toBe("A parent must stay above")
	})
})

describe("useManageQuestionsPage the editor's lists", () => {
	const bank = () => [
		question("group", { type: "group", displayOrder: 0 }),
		question("yes", { type: "yes_no", displayOrder: 1 }),
		question("pick", { type: "single_select", displayOrder: 2 }),
		question("ahead", { type: "autocomplete", displayOrder: 3 }),
		question("child", { type: "single_select", displayOrder: 4, choicesDependOnQuestionId: "pick" }),
		question("grouped", { type: "single_select", displayOrder: 5, groupedUnderQuestionId: "group" }),
		question("text", { type: "short_text", displayOrder: 6 }),
	]

	it("offers every yes/no and single-select as a condition, and every group as a group", async () => {
		const { result } = await load(bank())
		act(() => result.current.startNew())

		expect(result.current.editor?.conditionQuestions.map((q) => q.id)).toEqual(["yes", "pick", "child", "grouped"])
		expect(result.current.editor?.groupQuestions.map((q) => q.id)).toEqual(["group"])
	})

	it("never offers the question being edited to itself", async () => {
		const { result } = await load(bank())
		act(() => result.current.editQuestion(bank()[2]))

		expect(result.current.editor?.conditionQuestions.map((q) => q.id)).not.toContain("pick")
		act(() => result.current.editQuestion(bank()[0]))
		expect(result.current.editor?.groupQuestions).toEqual([])
	})

	it("offers every independent single-select and type-ahead as a choice parent for a new question", async () => {
		const { result } = await load(bank())
		act(() => result.current.startNew())

		expect(result.current.editor?.choiceParentQuestions?.map((q) => q.id)).toEqual(["pick", "ahead", "grouped"])
	})

	it("offers only the questions asked before the one being edited", async () => {
		const { result } = await load(bank())

		act(() => result.current.editQuestion(bank()[3]))
		expect(result.current.editor?.choiceParentQuestions?.map((q) => q.id)).toEqual(["pick", "grouped"])

		act(() => result.current.editQuestion(bank()[0]))
		expect(result.current.editor?.choiceParentQuestions).toEqual([])
	})

	it("places a grouped question on its group's page", async () => {
		const { result } = await load(bank())

		// "grouped" is asked on the group's page (position 0), before "yes".
		act(() => result.current.editQuestion(bank()[1]))
		expect(result.current.editor?.choiceParentQuestions?.map((q) => q.id)).toEqual(["grouped"])

		// Two questions on the same page are ordered within it.
		const sharedPage = [
			question("g", { type: "group", displayOrder: 1 }),
			question("first", { type: "single_select", displayOrder: 2, groupedUnderQuestionId: "g" }),
			question("second", { type: "single_select", displayOrder: 3, groupedUnderQuestionId: "g" }),
		]
		const shared = await load(sharedPage)
		act(() => shared.result.current.editQuestion(sharedPage[2]))
		expect(shared.result.current.editor?.choiceParentQuestions?.map((q) => q.id)).toEqual(["first"])
		act(() => shared.result.current.editQuestion(sharedPage[1]))
		expect(shared.result.current.editor?.choiceParentQuestions).toEqual([])
	})

	it("offers no choice parent to a question other questions' choices already depend on", async () => {
		const { result } = await load(bank())

		act(() => result.current.editQuestion(bank()[2]))

		expect(result.current.editor?.choiceParentQuestions?.map((q) => q.id)).not.toContain("child")
		expect(result.current.editor?.choiceParentQuestions).toEqual([])
		act(() => result.current.editQuestion(bank()[3]))
		expect(result.current.editor?.choiceParentQuestions?.map((q) => q.id)).toEqual(["pick", "grouped"])
	})
})

describe("ManageQuestionsPage", () => {
	it("renders its view with the view model", async () => {
		list.mockResolvedValue([question("a")])

		render(<ManageQuestionsPage />, { wrapper })

		expect(screen.getByText("page view")).toBeTruthy()
		await waitFor(() => expect((seen.props as { loading: boolean }).loading).toBe(false))
		expect((seen.props as { questions: QuestionView[] }).questions).toHaveLength(1)
	})
})
