import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { act, cleanup, fireEvent, render, renderHook, screen, waitFor } from "@testing-library/react"
import { ApiError } from "../api/adminQuestions"
import type { ImportedQuestionDraftView, PendingImportLogicView } from "../api/adminTypeformImport"
import { TypeformImportDialog, useTypeformImportDialog } from "./TypeformImportDialog"

const api = vi.hoisted(() => ({
	deletePendingImportLogic: vi.fn(),
	importTypeform: vi.fn(),
	listPendingImportLogic: vi.fn(),
}))

vi.mock("../api/adminTypeformImport", () => api)
vi.mock("../i18n/useLocale", () => ({ useLocale: () => ({ locale: "en-CA", setLocale: () => {}, t: (key: string) => key }) }))
vi.mock("./TypeformImportDialog.view", () => ({
	TypeformImportDialogView: (props: { onClose: () => void; error: string | null }) => (
		<button type="button" onClick={props.onClose}>
			{props.error ?? "no error"}
		</button>
	),
}))

const draft = { key: "q1", labelEn: "One" } as ImportedQuestionDraftView
const note = (id: string) => ({ id, fieldTitle: id }) as PendingImportLogicView
const english = new File(["{}"], "en.json")
const french = new File(["{}"], "fr.json")

beforeEach(() => {
	vi.resetAllMocks()
	api.listPendingImportLogic.mockResolvedValue([])
})

afterEach(() => {
	cleanup()
})

function setup(onReview = vi.fn()) {
	const hook = renderHook(() => useTypeformImportDialog({ onReview, onClose: () => {} }))
	return { ...hook, onReview }
}

describe("TypeformImportDialog", () => {
	it("hands its onClose and the view model to the view", async () => {
		const onClose = vi.fn()

		render(<TypeformImportDialog onReview={() => {}} onClose={onClose} />)
		fireEvent.click(await screen.findByRole("button"))

		expect(onClose).toHaveBeenCalledTimes(1)
	})
})

describe("useTypeformImportDialog", () => {
	it("loads the pending logic notes when it mounts", async () => {
		api.listPendingImportLogic.mockResolvedValue([note("n1")])

		const { result } = setup()

		await waitFor(() => expect(result.current.pendingLogic).toEqual([note("n1")]))
	})

	it("reports an API error's detail when the notes cannot be loaded", async () => {
		api.listPendingImportLogic.mockRejectedValue(new ApiError(500, "no notes"))

		const { result } = setup()

		await waitFor(() => expect(result.current.error).toBe("no notes"))
	})

	it("reports a generic message for any other failure", async () => {
		api.listPendingImportLogic.mockRejectedValue(new Error("boom"))

		const { result } = setup()

		await waitFor(() => expect(result.current.error).toBe("questions.import.error.unexpected"))
	})

	it("imports nothing until both files are chosen", async () => {
		const { result } = setup()
		await waitFor(() => expect(api.listPendingImportLogic).toHaveBeenCalled())

		await act(() => result.current.runImport())
		act(() => result.current.onEnglishChange(english))
		await act(() => result.current.runImport())
		act(() => {
			result.current.onEnglishChange(null)
			result.current.onFrenchChange(french)
		})
		await act(() => result.current.runImport())

		expect(api.importTypeform).not.toHaveBeenCalled()
		expect(result.current.english).toBeNull()
		expect(result.current.french).toBe(french)
	})

	it("imports the pair, shows the drafts and rejected fields, and refreshes the notes", async () => {
		api.importTypeform.mockResolvedValue({ drafts: [draft], rejected: [{ ref: "r", title: "R", typeformType: "x" }], pendingLogicNoteIds: [] })
		const { result } = setup()
		await waitFor(() => expect(api.listPendingImportLogic).toHaveBeenCalledTimes(1))
		act(() => {
			result.current.onEnglishChange(english)
			result.current.onFrenchChange(french)
		})

		await act(() => result.current.runImport())

		expect(api.importTypeform).toHaveBeenCalledWith(english, french)
		expect(result.current.drafts).toEqual([draft])
		expect(result.current.rejected).toHaveLength(1)
		expect(result.current.importing).toBe(false)
		expect(result.current.error).toBeNull()
		expect(api.listPendingImportLogic).toHaveBeenCalledTimes(2)
	})

	it("reports a failed import and stops importing", async () => {
		api.importTypeform.mockRejectedValue(new ApiError(400, "bad file"))
		const { result } = setup()
		act(() => {
			result.current.onEnglishChange(english)
			result.current.onFrenchChange(french)
		})

		await act(() => result.current.runImport())

		expect(result.current.error).toBe("bad file")
		expect(result.current.importing).toBe(false)
	})

	it("hands a draft to the caller and marks it reviewed, until the next import", async () => {
		api.importTypeform.mockResolvedValue({ drafts: [draft], rejected: [], pendingLogicNoteIds: [] })
		const { result, onReview } = setup()
		act(() => {
			result.current.onEnglishChange(english)
			result.current.onFrenchChange(french)
		})
		await act(() => result.current.runImport())

		act(() => result.current.reviewDraft(draft))

		expect(onReview).toHaveBeenCalledWith(draft)
		expect(result.current.reviewedKeys.has("q1")).toBe(true)

		await act(() => result.current.runImport())

		expect(result.current.reviewedKeys.size).toBe(0)
	})

	it("removes a resolved note from the list", async () => {
		api.listPendingImportLogic.mockResolvedValue([note("n1"), note("n2")])
		api.deletePendingImportLogic.mockResolvedValue(undefined)
		const { result } = setup()
		await waitFor(() => expect(result.current.pendingLogic).toHaveLength(2))

		await act(() => result.current.removePendingLogic("n1"))

		expect(api.deletePendingImportLogic).toHaveBeenCalledWith("n1")
		expect(result.current.pendingLogic).toEqual([note("n2")])
	})

	it("keeps the note and reports the error when removing it fails", async () => {
		api.listPendingImportLogic.mockResolvedValue([note("n1")])
		api.deletePendingImportLogic.mockRejectedValue(new ApiError(404, "gone"))
		const { result } = setup()
		await waitFor(() => expect(result.current.pendingLogic).toHaveLength(1))

		await act(() => result.current.removePendingLogic("n1"))

		expect(result.current.pendingLogic).toHaveLength(1)
		expect(result.current.error).toBe("gone")
	})
})
