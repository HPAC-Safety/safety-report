import { act, render, renderHook, screen } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"

const translate = vi.hoisted(() => vi.fn())
const guard = vi.hoisted(() => vi.fn())
const t = vi.hoisted(() => (key: string) => key)
vi.mock("../i18n/useLocale", () => ({ useLocale: () => ({ locale: "en-CA", t }) }))
vi.mock("../hooks/useUnsavedChangesGuard", () => ({ useUnsavedChangesGuard: guard }))
vi.mock("../api/adminQuestions", () => ({
	translate,
	ApiError: class ApiError extends Error {
		constructor(public detail: string) {
			super(detail)
		}
	},
}))
vi.mock("./ReviewActions.view", () => ({
	ReviewActionsView: (props: { mode: string }) => <p data-testid="view">{props.mode}</p>,
}))

import { ApiError } from "../api/adminQuestions"
import { ReviewActions, actionsFor, useReviewActions, type ReportDetail, type ReviewActionsProps } from "./ReviewActions"

const report = (over: Record<string, unknown> = {}) =>
	({ status: "pending", consent: true, summary: { aiSummaryEn: "EN", aiSummaryFr: "FR" }, ...over }) as unknown as ReportDetail
const base = (over: Partial<ReviewActionsProps> = {}): ReviewActionsProps => ({
	report: report(),
	busy: false,
	onSave: vi.fn().mockResolvedValue(true),
	onPublish: vi.fn(),
	onUnpublish: vi.fn().mockResolvedValue(true),
	onDelete: vi.fn(),
	...over,
})
const open = (props = base()) => {
	const hook = renderHook(() => useReviewActions(props))
	act(() => hook.result.current.openEditor())
	return hook
}

describe("useReviewActions", () => {
	beforeEach(() => {
		translate.mockReset()
		guard.mockClear()
	})

	it("starts on the action bar with the actions its state allows", () => {
		const { result } = renderHook(() => useReviewActions(base()))

		expect(result.current.mode).toBe("view")
		expect(result.current.actions).toEqual(actionsFor({ status: "pending", consent: true }))
		expect(guard).toHaveBeenLastCalledWith(false)
	})

	it("opens the editor on the current pair, or on blanks when there is none", () => {
		const withSummary = open()
		const without = open(base({ report: report({ summary: null }) }))

		expect(withSummary.result.current.mode).toBe("edit")
		expect(withSummary.result.current.draft).toEqual({ en: "EN", fr: "FR" })
		expect(without.result.current.draft).toEqual({ en: "", fr: "" })
	})

	it("guards unsaved typing, and nothing else", () => {
		const { result } = open()
		expect(guard).toHaveBeenLastCalledWith(false)

		act(() => result.current.type("en", "changed"))
		expect(guard).toHaveBeenLastCalledWith(true)

		act(() => result.current.cancel())
		expect(guard).toHaveBeenLastCalledWith(false)
	})

	it("guards a written unpublishing note only", () => {
		const { result } = renderHook(() => useReviewActions(base()))
		act(() => result.current.openUnpublish())
		expect(guard).toHaveBeenLastCalledWith(false)

		act(() => result.current.changeNote("   "))
		expect(guard).toHaveBeenLastCalledWith(false)
		act(() => result.current.changeNote("why"))
		expect(guard).toHaveBeenLastCalledWith(true)
	})

	it("offers saving only once a language differs and both are filled", () => {
		const { result } = open()
		expect(result.current.saveDisabled).toBe(true)

		act(() => result.current.type("en", "changed"))
		expect(result.current.saveDisabled).toBe(false)

		act(() => result.current.type("fr", "   "))
		expect(result.current.saveDisabled).toBe(true)

		act(() => result.current.type("fr", "FR"))
		act(() => result.current.type("en", " "))
		expect(result.current.saveDisabled).toBe(true)
	})

	it("disables saving while busy", () => {
		const { result } = open(base({ busy: true }))
		act(() => result.current.type("en", "changed"))

		expect(result.current.saveDisabled).toBe(true)
	})

	it("offers a translation only from a language the reviewer typed a change into", () => {
		const { result } = open()
		expect([result.current.changedEn, result.current.changedFr]).toEqual([false, false])

		act(() => result.current.type("en", "changed"))
		expect([result.current.changedEn, result.current.changedFr]).toEqual([true, false])

		act(() => result.current.type("en", "EN"))
		expect(result.current.changedEn).toBe(false)

		act(() => result.current.type("fr", ""))
		expect(result.current.changedFr).toBe(false)

		act(() => result.current.type("fr", "autre"))
		expect(result.current.changedFr).toBe(true)
	})

	it("fills an empty other language straight from a translation", async () => {
		translate.mockResolvedValue({ texts: ["TRANSLATED"] })
		const { result } = open()
		act(() => result.current.type("fr", ""))
		act(() => result.current.type("en", "changed"))

		await act(async () => result.current.translateFrom("en"))

		expect(translate).toHaveBeenCalledWith(["changed"], "en-CA", "fr-CA")
		expect(result.current.draft.fr).toBe("TRANSLATED")
		expect(result.current.proposal).toBeNull()
		expect(result.current.translating).toBe(false)
		expect(result.current.changedFr).toBe(false)
	})

	it("proposes a translation that would overwrite text, and applies it on request", async () => {
		translate.mockResolvedValue({ texts: ["PROPOSED"] })
		const { result } = open()
		act(() => result.current.type("fr", "autre"))

		await act(async () => result.current.translateFrom("fr"))
		expect(translate).toHaveBeenCalledWith(["autre"], "fr-CA", "en-CA")
		expect(result.current.proposal).toEqual({ target: "en", text: "PROPOSED" })
		expect(result.current.draft.en).toBe("EN")

		act(() => result.current.accept("en", "PROPOSED"))
		expect(result.current.draft.en).toBe("PROPOSED")
		expect(result.current.proposal).toBeNull()
	})

	it("keeps the current text when the proposal is declined", async () => {
		translate.mockResolvedValue({ texts: ["PROPOSED"] })
		const { result } = open()

		await act(async () => result.current.translateFrom("fr"))
		act(() => result.current.keepCurrent())

		expect(result.current.proposal).toBeNull()
		expect(result.current.draft.en).toBe("EN")
	})

	it("proposes an empty text when the translation returned none", async () => {
		translate.mockResolvedValue({ texts: [] })
		const { result } = open()

		await act(async () => result.current.translateFrom("fr"))

		expect(result.current.proposal).toEqual({ target: "en", text: "" })
	})

	it("shows the API's reason when translating fails, else a generic one", async () => {
		translate.mockRejectedValueOnce(new (ApiError as unknown as new (detail: string) => Error)("quota")).mockRejectedValueOnce(new Error("no"))
		const { result } = open()

		await act(async () => result.current.translateFrom("en"))
		expect(result.current.translateError).toBe("quota")
		expect(result.current.translating).toBe(false)

		await act(async () => result.current.translateFrom("en"))
		expect(result.current.translateError).toBe("reports.translate.error")

		act(() => result.current.openEditor())
		expect(result.current.translateError).toBeNull()
	})

	it("saves the pair with how each language was written, then returns to the bar", async () => {
		translate.mockResolvedValue({ texts: ["TRANSLATED"] })
		const props = base()
		const { result } = open(props)
		act(() => result.current.type("en", "changed"))
		await act(async () => result.current.translateFrom("en"))
		act(() => result.current.accept("fr", "TRANSLATED"))

		await act(async () => result.current.submitEdit())

		expect(props.onSave).toHaveBeenCalledWith("changed", "TRANSLATED", "human", "machine")
		expect(result.current.mode).toBe("view")
	})

	it("stays in the editor when the save was refused", async () => {
		const props = base({ onSave: vi.fn().mockResolvedValue(false) })
		const { result } = open(props)
		act(() => result.current.type("en", "changed"))

		await act(async () => result.current.submitEdit())

		expect(result.current.mode).toBe("edit")
	})

	it("unpublishes with the note, clears it, and returns to the bar", async () => {
		const props = base()
		const { result } = renderHook(() => useReviewActions(props))
		act(() => result.current.openUnpublish())
		act(() => result.current.changeNote("why"))

		await act(async () => result.current.submitUnpublish())

		expect(props.onUnpublish).toHaveBeenCalledWith("why")
		expect(result.current.mode).toBe("view")
		expect(result.current.note).toBe("")
	})

	it("keeps the note and the form when the unpublish was refused", async () => {
		const props = base({ onUnpublish: vi.fn().mockResolvedValue(false) })
		const { result } = renderHook(() => useReviewActions(props))
		act(() => result.current.openUnpublish())
		act(() => result.current.changeNote("why"))

		await act(async () => result.current.submitUnpublish())

		expect(result.current.mode).toBe("unpublish")
		expect(result.current.note).toBe("why")
	})
})

describe("ReviewActions", () => {
	it("renders its view with the view model", () => {
		render(<ReviewActions {...base()} />)

		expect(screen.getByTestId("view").textContent).toBe("view")
	})
})
