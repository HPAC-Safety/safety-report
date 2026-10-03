import { act, cleanup, fireEvent, render, renderHook, screen, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import {
	ApiError,
	approveTypeAheadValue,
	correctTypeAheadValue,
	listTypeAheadValuesAwaitingReview,
	mergeTypeAheadValue,
	removeTypeAheadValue,
	setTypeAheadValueParents,
	translate,
	translationAvailable,
	type TypeAheadValueView,
} from "../api/adminQuestions"
import { LocaleContext } from "../i18n/LocaleProvider"
import type { Locale } from "../i18n/locales"
import { ReviewTypeAheadValuesPage, useReviewTypeAheadValuesPage } from "./ReviewTypeAheadValuesPage"

vi.mock("../api/adminQuestions", async (importOriginal) => ({
	...(await importOriginal<typeof import("../api/adminQuestions")>()),
	listTypeAheadValuesAwaitingReview: vi.fn(),
	approveTypeAheadValue: vi.fn(),
	correctTypeAheadValue: vi.fn(),
	mergeTypeAheadValue: vi.fn(),
	setTypeAheadValueParents: vi.fn(),
	removeTypeAheadValue: vi.fn(),
	translate: vi.fn(),
	translationAvailable: vi.fn(),
}))
vi.mock("../components/TranslationDirectionSwitch", () => ({
	TranslationDirectionSwitch: (props: { direction: string; onChange: (direction: "toEnglish" | "toFrench") => void }) => (
		<button data-direction={props.direction} onClick={() => props.onChange(props.direction === "toFrench" ? "toEnglish" : "toFrench")}>
			flip
		</button>
	),
}))
vi.mock("./TypeAheadParentLink", () => ({
	TypeAheadParentLink: (props: { valueId: string; chosen: string[]; onChoose: (ids: string[]) => void; onRelink: (ids: string[]) => void }) => (
		<div data-parent={props.valueId} data-chosen={props.chosen.join()}>
			<button onClick={() => props.onChoose(["p2"])}>choose p2</button>
			<button onClick={() => props.onRelink(props.chosen)}>relink</button>
		</div>
	),
}))

afterEach(cleanup)

const wrapperFor = (locale: Locale = "en-CA") =>
	function Wrapper({ children }: { children: ReactNode }) {
		return <LocaleContext.Provider value={{ locale, setLocale: () => {}, t: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key) }}>{children}</LocaleContext.Provider>
	}

function value(id: string, over: Partial<TypeAheadValueView> = {}): TypeAheadValueView {
	return {
		id,
		questionId: "q1",
		questionLabelEn: "Aircraft",
		questionLabelFr: "Aéronef",
		labelEn: `en-${id}`,
		labelFr: `fr-${id}`,
		typedIn: null,
		isRemoved: false,
		answerCount: 3,
		addedAt: null,
		mergeTargets: [],
		aliases: [],
		parent: null,
		...over,
	}
}

const mocked = {
	list: vi.mocked(listTypeAheadValuesAwaitingReview),
	approve: vi.mocked(approveTypeAheadValue),
	correct: vi.mocked(correctTypeAheadValue),
	merge: vi.mocked(mergeTypeAheadValue),
	relink: vi.mocked(setTypeAheadValueParents),
	remove: vi.mocked(removeTypeAheadValue),
	translate: vi.mocked(translate),
	available: vi.mocked(translationAvailable),
}

function queue(...values: TypeAheadValueView[]) {
	mocked.list.mockResolvedValue({ values, count: values.length })
}

beforeEach(() => {
	for (const fn of Object.values(mocked)) fn.mockReset()
	queue(value("v1"))
	mocked.available.mockResolvedValue({ available: true })
	for (const fn of [mocked.approve, mocked.correct, mocked.merge, mocked.relink, mocked.remove]) fn.mockResolvedValue(undefined)
})

async function mountHook(locale: Locale = "en-CA") {
	const view = renderHook(() => useReviewTypeAheadValuesPage(), { wrapper: wrapperFor(locale) })
	await waitFor(() => expect(view.result.current.loading).toBe(false))
	const card = (index = 0) => view.result.current.groups[0].cards[index]
	return { ...view, card }
}

describe("the review page", () => {
	it("shows loading, then the queue grouped by question", async () => {
		queue(value("v1", { typedIn: "en-CA" }), value("v2", { questionId: "q0", questionLabelEn: "Airport", isRemoved: true, aliases: [{ labelEn: "Alias", labelFr: null }] }))
		render(<ReviewTypeAheadValuesPage />, { wrapper: wrapperFor() })
		expect(screen.getByText("typeAheadValues.loading")).toBeTruthy()
		await screen.findByText("Aircraft")
		expect(screen.getAllByRole("heading", { level: 2 }).map((h) => h.textContent)).toEqual(["Aircraft", "Airport"].sort())
		expect(screen.getAllByTestId("type-ahead-value-wording").map((p) => p.textContent).sort()).toEqual(["en-v1", "en-v2"])
		expect(screen.getByText(/typeAheadValues.typedIn/)).toBeTruthy()
		expect(screen.getByText(/typeAheadValues.removedBadge/)).toBeTruthy()
		expect(screen.getByTestId("type-ahead-value-aliases").textContent).toBe('typeAheadValues.aliases {"aliases":"Alias"}')
	})

	it("says so when nothing waits", async () => {
		queue()
		render(<ReviewTypeAheadValuesPage />, { wrapper: wrapperFor() })
		expect(await screen.findByText("typeAheadValues.empty")).toBeTruthy()
	})

	it("shows the server's detail when the queue cannot be read, or a generic message", async () => {
		mocked.list.mockRejectedValueOnce(new ApiError(500, "Queue broke"))
		render(<ReviewTypeAheadValuesPage />, { wrapper: wrapperFor() })
		expect((await screen.findByRole("alert")).textContent).toBe("Queue broke")
		cleanup()
		mocked.list.mockRejectedValueOnce(new Error("offline"))
		render(<ReviewTypeAheadValuesPage />, { wrapper: wrapperFor() })
		expect((await screen.findByRole("alert")).textContent).toBe("typeAheadValues.error.unexpected")
	})

	it("approves, removes and merges a value, then refetches", async () => {
		queue(value("v1", { mergeTargets: [{ id: "v2", labelEn: "Other", labelFr: null, pin: "none" }] }))
		render(<ReviewTypeAheadValuesPage />, { wrapper: wrapperFor() })
		await screen.findByText("en-v1")
		fireEvent.click(screen.getByRole("button", { name: "typeAheadValues.approve" }))
		await waitFor(() => expect(mocked.approve).toHaveBeenCalledWith("v1"))
		await waitFor(() => expect(mocked.list).toHaveBeenCalledTimes(2))
		fireEvent.click(screen.getByRole("button", { name: "typeAheadValues.remove" }))
		await waitFor(() => expect(mocked.remove).toHaveBeenCalledWith("v1"))
		const merge = screen.getByRole<HTMLButtonElement>("button", { name: "typeAheadValues.merge" })
		expect(merge.disabled).toBe(true)
		fireEvent.change(screen.getByRole("combobox"), { target: { value: "v2" } })
		fireEvent.click(merge)
		await waitFor(() => expect(mocked.merge).toHaveBeenCalledWith("v1", "v2"))
	})

	it("does not offer Remove for an already removed value, nor Merge without targets", async () => {
		queue(value("v1", { isRemoved: true }))
		render(<ReviewTypeAheadValuesPage />, { wrapper: wrapperFor() })
		await screen.findByText("en-v1")
		expect(screen.queryByRole("button", { name: "typeAheadValues.remove" })).toBeNull()
		expect(screen.queryByRole("combobox")).toBeNull()
	})

	it("shows an action's failure", async () => {
		mocked.approve.mockRejectedValue(new ApiError(409, "Already approved"))
		render(<ReviewTypeAheadValuesPage />, { wrapper: wrapperFor() })
		await screen.findByText("en-v1")
		fireEvent.click(screen.getByRole("button", { name: "typeAheadValues.approve" }))
		expect((await screen.findByRole("alert")).textContent).toBe("Already approved")
	})

	it("corrects a value in place: open, edit both languages, save", async () => {
		render(<ReviewTypeAheadValuesPage />, { wrapper: wrapperFor() })
		await screen.findByText("en-v1")
		fireEvent.click(screen.getByRole("button", { name: "typeAheadValues.correct" }))
		const [en, fr] = screen.getAllByRole<HTMLInputElement>("textbox")
		expect([en.value, fr.value]).toEqual(["en-v1", "fr-v1"])
		fireEvent.change(en, { target: { value: "Fixed" } })
		fireEvent.change(fr, { target: { value: "Corrigé" } })
		fireEvent.click(screen.getByRole("button", { name: "typeAheadValues.saveCorrection" }))
		await waitFor(() => expect(mocked.correct).toHaveBeenCalledWith("v1", "Fixed", "Corrigé"))
		await waitFor(() => expect(screen.queryAllByRole("textbox")).toHaveLength(0))
	})

	it("cancels a correction and cannot save an empty one", async () => {
		queue(value("v1", { labelEn: null, labelFr: null }))
		render(<ReviewTypeAheadValuesPage />, { wrapper: wrapperFor() })
		await screen.findByRole("button", { name: "typeAheadValues.correct" })
		fireEvent.click(screen.getByRole("button", { name: "typeAheadValues.correct" }))
		expect(screen.getByRole<HTMLButtonElement>("button", { name: "typeAheadValues.saveCorrection" }).disabled).toBe(true)
		fireEvent.click(screen.getByRole("button", { name: "typeAheadValues.cancel" }))
		expect(screen.queryAllByRole("textbox")).toHaveLength(0)
	})

	it("translates a draft in the chosen direction", async () => {
		mocked.translate.mockResolvedValue({ texts: ["Bonjour"] })
		render(<ReviewTypeAheadValuesPage />, { wrapper: wrapperFor() })
		await screen.findByText("en-v1")
		fireEvent.click(screen.getByRole("button", { name: "typeAheadValues.correct" }))
		const [en, fr] = screen.getAllByRole<HTMLInputElement>("textbox")
		fireEvent.change(fr, { target: { value: "" } })
		fireEvent.click(screen.getByRole("button", { name: "typeAheadValues.translate.action" }))
		await waitFor(() => expect(fr.value).toBe("Bonjour"))
		expect(mocked.translate).toHaveBeenCalledWith(["en-v1"], "en-CA", "fr-CA")
		expect(en.value).toBe("en-v1")
		fireEvent.click(screen.getByText("flip"))
		fireEvent.change(fr, { target: { value: "Salut" } })
		mocked.translate.mockResolvedValue({ texts: ["Hi"] })
		fireEvent.click(screen.getByRole("button", { name: "typeAheadValues.translate.action" }))
		await waitFor(() => expect(en.value).toBe("Hi"))
	})

	it("withholds Translate when the server has none", async () => {
		mocked.available.mockRejectedValue(new Error("no"))
		render(<ReviewTypeAheadValuesPage />, { wrapper: wrapperFor() })
		await screen.findByText("en-v1")
		fireEvent.click(screen.getByRole("button", { name: "typeAheadValues.correct" }))
		expect(screen.getByRole<HTMLButtonElement>("button", { name: "typeAheadValues.translate.action" }).disabled).toBe(true)
		expect(screen.getByText("typeAheadValues.translate.unavailable")).toBeTruthy()
	})

	it("shows the parent link with its live parents, lets the reviewer choose, and relinks", async () => {
		queue(
			value("v1", {
				parent: {
					questionId: "pq",
					questionLabelEn: "P",
					questionLabelFr: "P",
					parentChoiceIds: ["p1", "gone"],
					choices: [
						{ id: "p1", labelEn: "One", labelFr: null, pin: "none" },
						{ id: "p2", labelEn: "Two", labelFr: null, pin: "none" },
					],
				},
			}),
		)
		render(<ReviewTypeAheadValuesPage />, { wrapper: wrapperFor() })
		await screen.findByText("en-v1")
		expect(document.querySelector("[data-parent]")!.getAttribute("data-chosen")).toBe("p1")
		fireEvent.click(screen.getByText("relink"))
		await waitFor(() => expect(mocked.relink).toHaveBeenCalledWith("v1", ["p1"]))
		fireEvent.click(screen.getByText("choose p2"))
		expect(document.querySelector("[data-parent]")!.getAttribute("data-chosen")).toBe("p2")
	})
})

describe("useReviewTypeAheadValuesPage", () => {
	it("reads French wording and headings for a French reader, falling back to the other language", async () => {
		queue(value("v1", { labelFr: null, aliases: undefined as unknown as TypeAheadValueView["aliases"] }))
		const { result, card } = await mountHook("fr-CA")
		expect(result.current.groups[0].heading).toBe("Aéronef")
		expect(card().text).toBe("en-v1")
		expect(card().hasAliases).toBe(false)
		expect(result.current.wording({ labelEn: null, labelFr: "x" })).toBe("x")
	})

	it("lists merge targets sorted in the reader's language", async () => {
		queue(value("v1", { mergeTargets: [{ id: "b", labelEn: "Beta", labelFr: null, pin: "none" }, { id: "a", labelEn: "Alpha", labelFr: null, pin: "none" }] }))
		const { card } = await mountHook()
		expect(card().mergeOptions).toEqual([{ id: "a", text: "Alpha" }, { id: "b", text: "Beta" }])
	})

	it("orders the values of one question alphabetically in the reader's language", async () => {
		queue(value("v1", { labelEn: "Zulu" }), value("v2", { labelEn: "Alpha" }))
		const { result } = await mountHook()
		expect(result.current.groups[0].cards.map((card) => card.text)).toEqual(["Alpha", "Zulu"])
	})

	it("leaves a value with no parent with no chosen parents", async () => {
		const { card } = await mountHook()
		expect(card().parentChosen).toEqual([])
	})

	it("ignores Translate on a value with no correction open", async () => {
		const { card } = await mountHook()
		await act(async () => card().onTranslate())
		expect(mocked.translate).not.toHaveBeenCalled()
		expect(card().translateDisabled).toBe(true)
	})

	it("treats a translation with no text as empty", async () => {
		mocked.translate.mockResolvedValue({ texts: [] })
		const { card, result } = await mountHook()
		act(() => card().onCorrect())
		await act(async () => card().onTranslate())
		expect(card().draft?.labelFr).toBe("")
		expect(result.current.error).toBeNull()
	})

	it("drops a translation made stale by an edit or a flip, and stops showing it as working", async () => {
		let finish: (value: { texts: string[] }) => void = () => {}
		mocked.translate.mockImplementation(() => new Promise((resolve) => (finish = resolve as never)))
		const { card } = await mountHook()
		act(() => card().onCorrect())
		act(() => card().onTranslate())
		expect(card().draft?.translating).toBe(true)
		act(() => card().onLabelEn("edited"))
		await act(async () => finish({ texts: ["Stale"] }))
		expect(card().draft).toMatchObject({ labelEn: "edited", labelFr: "fr-v1", translating: false })

		act(() => card().onTranslate())
		act(() => card().onDirection("toEnglish"))
		await act(async () => finish({ texts: ["Stale"] }))
		expect(card().draft).toMatchObject({ labelEn: "edited", translating: false })
	})

	it("drops a translation whose correction was cancelled while it was out", async () => {
		let finish: (value: { texts: string[] }) => void = () => {}
		mocked.translate.mockImplementation(() => new Promise((resolve) => (finish = resolve as never)))
		const { card } = await mountHook()
		act(() => card().onCorrect())
		const stale = card()
		act(() => stale.onTranslate())
		act(() => stale.onCancel())
		await act(async () => finish({ texts: ["Late"] }))
		expect(card().draft).toBeUndefined()
		act(() => stale.onDirection("toEnglish"))
		expect(card().draft).toBeUndefined()
	})

	it("starts no translation for a correction cancelled before it ran", async () => {
		const { card } = await mountHook()
		act(() => card().onCorrect())
		const stale = card()
		act(() => stale.onCancel())
		mocked.translate.mockRejectedValue(new Error("late"))
		await act(async () => stale.onTranslate())
		expect(card().draft).toBeUndefined()
	})

	it("shows a translation failure on the draft, from the server or generic", async () => {
		mocked.translate.mockRejectedValueOnce(new ApiError(502, "Provider down")).mockRejectedValueOnce(new Error("offline"))
		const { card } = await mountHook()
		act(() => card().onCorrect())
		await act(async () => card().onTranslate())
		expect(card().draft?.translationError).toBe("Provider down")
		await act(async () => card().onTranslate())
		expect(card().draft?.translationError).toBe("typeAheadValues.translate.failed")
	})
})
