import { act, render, renderHook, screen, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { MemoryRouter, Route, Routes, useLocation, useNavigate } from "react-router-dom"
import { beforeEach, describe, expect, it, vi } from "vitest"

import type { PublicQuestionView } from "../api/publicQuestions"
import { fetchCurrentQuestions } from "../api/publicQuestions"
import { SubmissionNetworkError, SubmissionRejectedError, submitReport } from "../api/reportSubmission"
import { deleteUpload } from "../api/uploads"
import type { UnsavedChangesOptions } from "../hooks/useUnsavedChangesGuard"
import { useUnsavedChangesGuard } from "../hooks/useUnsavedChangesGuard"
import type { Attachment } from "./AttachmentField"
import { readDraft, writeDraft } from "./draft"
import { ReportForm, useReportForm } from "./ReportForm"

const locales = vi.hoisted(() => {
	const english = (key: string, params?: Record<string, string | number>) => (params ? `${key}|${JSON.stringify(params)}` : key)
	const french = (key: string, params?: Record<string, string | number>) => `fr:${english(key, params)}`
	return { english, french, current: { locale: "en-CA", t: english } }
})

vi.mock("../i18n/useLocale", () => ({ useLocale: () => locales.current }))
vi.mock("../api/publicQuestions", () => ({ fetchCurrentQuestions: vi.fn() }))
vi.mock("../api/reportSubmission", async (importOriginal) => ({
	...(await importOriginal<typeof import("../api/reportSubmission")>()),
	submitReport: vi.fn(),
}))
vi.mock("../api/uploads", async (importOriginal) => ({
	...(await importOriginal<typeof import("../api/uploads")>()),
	deleteUpload: vi.fn(() => Promise.resolve()),
}))
vi.mock("../hooks/useUnsavedChangesGuard", () => ({ useUnsavedChangesGuard: vi.fn() }))
vi.mock("./ReportForm.view", () => ({
	ReportFormView: (props: { loadStatus: string }) => <div data-testid="view" data-status={props.loadStatus} />,
}))

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

const questions = [
	question("intro", "statement"),
	question("name", "short_text", { isRequired: true }),
	question("mail", "email"),
	question("files", "file_upload"),
	question("last", "short_text"),
]

const uploaded = (uploadId: string): Attachment => ({ key: `k-${uploadId}`, name: `${uploadId}.pdf`, size: 5, status: "uploaded", uploadId })
const text = (value: string) => ({ kind: "value" as const, value })

const probe: { navigate: ReturnType<typeof useNavigate>; path: string } = { navigate: () => {}, path: "" }

function Probe() {
	probe.navigate = useNavigate()
	probe.path = useLocation().pathname
	return null
}

function routed(initial: string) {
	return function Wrapper({ children }: { children: ReactNode }) {
		return (
			<MemoryRouter initialEntries={[initial]}>
				<Probe />
				<Routes>
					<Route path="/report/:stepKey?" element={<>{children}</>} />
				</Routes>
			</MemoryRouter>
		)
	}
}

async function renderForm(initial = "/report") {
	const rendered = renderHook(() => useReportForm(), { wrapper: routed(initial) })
	await waitFor(() => expect(rendered.result.current.loadStatus).toBe("ready"))
	await waitFor(() => expect(rendered.result.current.currentStep).not.toBeNull())
	return rendered
}

async function go(path: string) {
	await act(async () => probe.navigate(path))
}

beforeEach(() => {
	vi.clearAllMocks()
	localStorage.clear()
	locales.current = { locale: "en-CA", t: locales.english }
	vi.mocked(fetchCurrentQuestions).mockResolvedValue(questions)
})

describe("loading the questions", () => {
	it("reports loading, then ready with the introduction as the first page", async () => {
		const { result } = renderHook(() => useReportForm(), { wrapper: routed("/report") })
		expect(result.current.loadStatus).toBe("loading")
		expect(result.current.currentStep).toBeNull()

		await waitFor(() => expect(result.current.currentStep?.kind).toBe("intro"))
		expect(result.current.loadStatus).toBe("ready")
		expect(result.current.visibleCount).toBe(5)
		expect(result.current.isFirst).toBe(true)
		expect(result.current.isLast).toBe(false)
	})

	it("goes nowhere when asked to page before the form has loaded", () => {
		const { result } = renderHook(() => useReportForm(), { wrapper: routed("/report") })

		act(() => result.current.onNext())

		expect(probe.path).toBe("/report")
		expect(result.current.blocking).toEqual([])
	})

	it("reports an error when the questions cannot be fetched", async () => {
		vi.mocked(fetchCurrentQuestions).mockRejectedValue(new Error("down"))
		const { result } = renderHook(() => useReportForm(), { wrapper: routed("/report") })

		await waitFor(() => expect(result.current.loadStatus).toBe("error"))
	})

	it("ignores an answer that arrives after the form has gone", async () => {
		let resolve: (value: PublicQuestionView[]) => void = () => {}
		vi.mocked(fetchCurrentQuestions).mockReturnValue(new Promise((done) => (resolve = done)))
		const { unmount } = renderHook(() => useReportForm(), { wrapper: routed("/report") })
		unmount()

		await act(async () => {
			resolve(questions)
			await Promise.resolve()
		})
		expect(readDraft().draft).toBeNull()
	})

	it("ignores a failure that arrives after the form has gone", async () => {
		let reject: (reason: Error) => void = () => {}
		vi.mocked(fetchCurrentQuestions).mockReturnValue(new Promise((_, failed) => (reject = failed)))
		const { unmount } = renderHook(() => useReportForm(), { wrapper: routed("/report") })
		unmount()

		await act(async () => {
			reject(new Error("late"))
			await Promise.resolve()
		})
		expect(readDraft().draft).toBeNull()
	})

	it("puts a form reached on a page address back at the form address", async () => {
		await renderForm("/report/intro")

		await waitFor(() => expect(probe.path).toBe("/report"))
	})

	it("fades each page in after it is shown", async () => {
		const { result } = await renderForm()

		await waitFor(() => expect(result.current.entering).toBe(true))
	})
})

describe("the saved report on offer", () => {
	it("offers nothing when the browser holds no report", async () => {
		const { result } = await renderForm()

		expect(result.current.pendingDraft).toBe(false)
		expect(result.current.clearedNotice).toBe(false)
		expect(localStorage.length).toBe(0)
	})

	it("offers a saved report once, worded in the reader's language, and never restores it unasked", async () => {
		writeDraft({ locale: "en-CA", answers: { "rev-name": text("Ada") }, attachments: { "rev-files": [{ uploadId: "u1", name: "u1.pdf", size: 5 }] }, stepKey: "name" })
		const { result, rerender } = await renderForm()

		expect(result.current.pendingDraft).toBe(true)
		expect(result.current.pendingRows.map((row) => row.revisionId)).toEqual(["rev-name", "rev-files"])
		expect(result.current.answers).toEqual({})

		locales.current = { locale: "fr-CA", t: locales.french }
		rerender()
		expect(result.current.pendingDraft).toBe(true)
		expect(deleteUpload).not.toHaveBeenCalled()
	})

	it("continues the saved report: answers, files, and the page the reporter was on", async () => {
		writeDraft({
			locale: "en-CA",
			answers: { "rev-name": text("Ada"), "rev-gone": text("old") },
			attachments: { "rev-files": [{ uploadId: "u1", name: "u1.pdf", size: 5 }], "rev-gone": [{ uploadId: "u9", name: "u9.pdf", size: 5 }] },
			stepKey: "name",
		})
		const { result } = await renderForm()

		act(() => result.current.onContinue())

		expect(result.current.pendingDraft).toBe(false)
		expect(result.current.answers).toEqual({ "rev-name": text("Ada") })
		expect(result.current.attachments).toEqual({
			"rev-files": [{ key: "restored-u1", name: "u1.pdf", size: 5, status: "uploaded", uploadId: "u1" }],
		})
		expect(result.current.clearedNotice).toBe(true)
		expect(deleteUpload).toHaveBeenCalledTimes(1)
		expect(deleteUpload).toHaveBeenCalledWith("u9")
		await waitFor(() => expect(probe.path).toBe("/report/name"))
	})

	it("continues a report saved by revision rather than by page key", async () => {
		localStorage.setItem(
			"hpac.report.draft",
			JSON.stringify({ locale: "en-CA", answers: { "rev-name": text("Ada") }, stepRevisionId: "rev-last", savedAtMs: Date.now() }),
		)
		const { result } = await renderForm()

		act(() => result.current.onContinue())

		await waitFor(() => expect(probe.path).toBe("/report/last"))
		expect(result.current.clearedNotice).toBe(false)
	})

	it("restores no files, and does not throw, from a saved report with no attachments map or none for the question", async () => {
		for (const attachments of [undefined, { "rev-other": [{ uploadId: "u3", name: "u3.pdf", size: 1 }] }]) {
			localStorage.clear()
			localStorage.setItem(
				"hpac.report.draft",
				JSON.stringify({ locale: "en-CA", answers: { "rev-name": text("Ada"), "rev-files": text("stray") }, attachments, savedAtMs: Date.now() }),
			)
			const { result, unmount } = await renderForm()

			act(() => result.current.onContinue())

			expect(result.current.answers["rev-name"]).toEqual(text("Ada"))
			expect(result.current.attachments).toEqual({})
			unmount()
		}
	})

	it("stays where it is when the saved page is no longer on the form", async () => {
		writeDraft({ locale: "en-CA", answers: { "rev-name": text("Ada") }, stepKey: "removed" })
		const { result } = await renderForm()

		act(() => result.current.onContinue())

		expect(result.current.answers).toEqual({ "rev-name": text("Ada") })
		expect(probe.path).toBe("/report")
	})

	it("does nothing when asked to continue with nothing on offer", async () => {
		const { result } = await renderForm()

		act(() => result.current.onContinue())

		expect(result.current.answers).toEqual({})
	})

	it("starts over: erases the saved report and its files", async () => {
		writeDraft({ locale: "en-CA", answers: { "rev-name": text("Ada") }, attachments: { "rev-files": [{ uploadId: "u1", name: "u1.pdf", size: 5 }] } })
		const { result } = await renderForm()

		act(() => result.current.onStartOver())

		expect(result.current.pendingDraft).toBe(false)
		expect(deleteUpload).toHaveBeenCalledWith("u1")
		expect(readDraft().draft).toBeNull()
	})

	it("starting over with nothing on offer only clears the browser", async () => {
		const { result } = await renderForm()

		act(() => result.current.onStartOver())

		expect(deleteUpload).not.toHaveBeenCalled()
	})

	it("drops a saved report with nothing on this form to continue, and says what was cleared", async () => {
		writeDraft({ locale: "en-CA", answers: { "rev-old": text("x") }, attachments: { "rev-old-files": [{ uploadId: "u7", name: "u7.pdf", size: 1 }] } })
		const { result } = await renderForm()

		expect(result.current.pendingDraft).toBe(false)
		expect(result.current.clearedNotice).toBe(true)
		expect(deleteUpload).toHaveBeenCalledWith("u7")
		expect(readDraft().draft).toBeNull()
	})

	it("drops an empty saved report without a notice", async () => {
		writeDraft({ locale: "en-CA", answers: {} })
		const { result } = await renderForm()

		expect(result.current.pendingDraft).toBe(false)
		expect(result.current.clearedNotice).toBe(false)
		expect(readDraft().draft).toBeNull()
	})

	it("erases the files of a saved report that has expired", async () => {
		const long = Date.now() - 16 * 24 * 60 * 60 * 1000
		localStorage.setItem(
			"hpac.report.draft",
			JSON.stringify({ locale: "en-CA", answers: {}, attachments: { "rev-files": [{ uploadId: "u-old", name: "o.pdf", size: 1 }] }, startedAtMs: long, savedAtMs: long }),
		)
		const { result } = await renderForm()

		expect(result.current.pendingDraft).toBe(false)
		expect(deleteUpload).toHaveBeenCalledWith("u-old")
	})
})

describe("keeping the report in the browser", () => {
	it("writes nothing for a form nobody has touched", async () => {
		await renderForm()

		expect(localStorage.getItem("hpac.report.draft")).toBeNull()
	})

	it("saves an answer with the page the reporter is on, and removes it when cleared", async () => {
		const { result } = await renderForm()

		act(() => result.current.onAnswer("rev-name", text("Ada")))
		expect(readDraft().draft).toMatchObject({ locale: "en-CA", answers: { "rev-name": text("Ada") }, stepKey: "intro" })

		act(() => result.current.onAnswer("rev-name", undefined))
		expect(result.current.answers).toEqual({})
		expect(readDraft().draft).toMatchObject({ answers: {} })
	})

	it("saves finished files, and rewrites the report when the last one is removed", async () => {
		const { result } = await renderForm()

		act(() => result.current.onAttachments("rev-files", () => [uploaded("u1")]))
		expect(readDraft().draft?.attachments).toEqual({ "rev-files": [{ uploadId: "u1", name: "u1.pdf", size: 5 }] })

		act(() => result.current.onAttachments("rev-files", () => []))
		expect(readDraft().draft?.attachments).toEqual({})
	})

	it("caps what the files leave room for at five in all", async () => {
		const { result } = await renderForm()
		expect(result.current.attachmentRoom).toBe(5)

		act(() => result.current.onAttachments("rev-files", () => [uploaded("a"), uploaded("b"), { ...uploaded("c"), status: "expired" }]))
		expect(result.current.attachmentRoom).toBe(3)
	})
})

describe("leaving the form", () => {
	function lastGuard() {
		const calls = vi.mocked(useUnsavedChangesGuard).mock.calls
		const [dirty, options] = calls[calls.length - 1]
		return { dirty, options: options as UnsavedChangesOptions }
	}

	it("is clean until there is something to lose, and never blocks the form's own pages", async () => {
		const { result } = await renderForm()
		expect(lastGuard()).toMatchObject({ dirty: false, options: { withinPath: "/report", unloadPrompt: false } })

		act(() => result.current.onAnswer("rev-name", text("Ada")))
		expect(lastGuard().dirty).toBe(true)
	})

	it("is dirty for a file row alone, and for an upload in flight alone", async () => {
		const { result } = await renderForm()

		act(() => result.current.onUploading("rev-files", true))
		expect(lastGuard()).toMatchObject({ dirty: true, options: { unloadPrompt: true } })

		act(() => result.current.onUploading("rev-files", false))
		expect(lastGuard().dirty).toBe(false)

		act(() => result.current.onAttachments("rev-files", () => [uploaded("u1")]))
		expect(lastGuard().dirty).toBe(true)
	})

	it("words the leave dialog with the day the saved report expires", async () => {
		const { result } = await renderForm()
		expect(lastGuard().options.copy?.()).toBeUndefined()

		act(() => result.current.onAnswer("rev-name", text("Ada")))
		const copy = lastGuard().options.copy?.()
		expect(copy).toMatchObject({ title: "report.leave.title", leave: "unsavedChanges.leave", stay: "report.leave.keepWorking" })
		expect(copy?.body).toContain("report.leave.body")
		expect(copy?.body).not.toContain("report.leave.uploading")
	})

	it("adds a warning to the leave dialog while a file is still uploading", async () => {
		const { result } = await renderForm()
		act(() => result.current.onAnswer("rev-name", text("Ada")))
		act(() => result.current.onUploading("rev-files", true))

		expect(lastGuard().options.copy?.()?.body).toContain("report.leave.uploading")
	})
})

describe("paging", () => {
	it("moves forward and back, one history entry per page", async () => {
		const { result } = await renderForm()

		act(() => result.current.onNext())
		await waitFor(() => expect(probe.path).toBe("/report/name"))
		expect(result.current.isFirst).toBe(false)

		act(() => result.current.onBack())
		await waitFor(() => expect(probe.path).toBe("/report"))
	})

	it("does not go back from the first page", async () => {
		const { result } = await renderForm()

		act(() => result.current.onBack())

		expect(probe.path).toBe("/report")
	})

	it("refuses to leave a page with an unanswered required question, and names it", async () => {
		const { result } = await renderForm()
		await go("/report/name")

		act(() => result.current.onNext())

		expect(probe.path).toBe("/report/name")
		expect(result.current.blocking.map((entry) => entry.key)).toEqual(["name"])
		expect(result.current.blockingMessages.get("rev-name")).toBe("report.required.error")
		expect(result.current.anyUnanswered).toBe(true)

		act(() => result.current.onAnswer("rev-name", text("Ada")))
		expect(result.current.blocking).toEqual([])
		act(() => result.current.onNext())
		await waitFor(() => expect(probe.path).toBe("/report/mail"))
	})

	it("says what a malformed answer needs instead of calling it unanswered", async () => {
		const { result } = await renderForm()
		act(() => result.current.onAnswer("rev-name", text("Ada")))
		act(() => result.current.onAnswer("rev-mail", text("not-an-email")))
		await go("/report/mail")

		act(() => result.current.onNext())

		expect(result.current.blockingMessages.get("rev-mail")).toBe("report.email.invalid")
		expect(result.current.anyUnanswered).toBe(false)
	})

	it("holds the Next button while a file is uploading", async () => {
		const { result } = await renderForm()
		act(() => result.current.onUploading("rev-files", true))
		act(() => result.current.onUploading("rev-files", true))
		expect(result.current.anyUploading).toBe(true)

		act(() => result.current.onNext())

		expect(probe.path).toBe("/report")
	})

	it("sends a reporter who goes Forward past an unanswered required page back to it", async () => {
		const { result } = await renderForm()

		await go("/report/last")

		await waitFor(() => expect(probe.path).toBe("/report/name"))
		expect(result.current.blocking.map((entry) => entry.key)).toEqual(["name"])
	})

	it("sends a reporter who goes to a page that is not on the form back to the form address", async () => {
		await renderForm()

		await go("/report/nowhere")

		await waitFor(() => expect(probe.path).toBe("/report"))
	})

	it("marks the last visible page, past which there is nowhere to go", async () => {
		const { result } = await renderForm()
		act(() => result.current.onAnswer("rev-name", text("Ada")))

		await go("/report/last")

		await waitFor(() => expect(result.current.isLast).toBe(true))
		act(() => result.current.onNext())
		expect(probe.path).toBe("/report/last")
	})
})

describe("submitting", () => {
	it("sends the answers and shows the confirmation", async () => {
		vi.mocked(submitReport).mockResolvedValue({ id: "r1" } as Awaited<ReturnType<typeof submitReport>>)
		const { result } = await renderForm()
		act(() => result.current.onAnswer("rev-name", text("Ada")))

		await act(async () => result.current.onSubmit())

		expect(submitReport).toHaveBeenCalledWith(
			"en-CA",
			expect.arrayContaining([{ questionRevisionId: "rev-name", value: "Ada", choices: null, attachments: null }]),
		)
		expect(result.current.submit).toEqual({ status: "submitted", id: "r1" })
		expect(readDraft().draft).toBeNull()
	})

	it("stops a page with an unanswered required question from being submitted", async () => {
		const { result } = await renderForm()
		await go("/report/name")

		await act(async () => result.current.onSubmit())

		expect(submitReport).not.toHaveBeenCalled()
		expect(result.current.blocking).toHaveLength(1)
	})

	it("does not submit twice, or while a file is uploading", async () => {
		let finish: (value: Awaited<ReturnType<typeof submitReport>>) => void = () => {}
		vi.mocked(submitReport).mockReturnValue(new Promise((done) => (finish = done)))
		const { result } = await renderForm()

		act(() => result.current.onUploading("rev-files", true))
		await act(async () => result.current.onSubmit())
		expect(submitReport).not.toHaveBeenCalled()
		act(() => result.current.onUploading("rev-files", false))

		act(() => void result.current.onSubmit())
		expect(result.current.submit.status).toBe("submitting")
		await act(async () => result.current.onSubmit())
		expect(submitReport).toHaveBeenCalledTimes(1)

		await act(async () => {
			finish({ id: "r2" } as Awaited<ReturnType<typeof submitReport>>)
			await Promise.resolve()
		})
		expect(result.current.submit).toEqual({ status: "submitted", id: "r2" })
	})

	it("keeps the report and says so when the network fails", async () => {
		vi.mocked(submitReport).mockRejectedValue(new SubmissionNetworkError())
		const { result } = await renderForm()
		act(() => result.current.onAnswer("rev-name", text("Ada")))

		await act(async () => result.current.onSubmit())

		expect(result.current.submit).toEqual({ status: "failed", message: "report.error.network", keepsLocalState: true })
		expect(readDraft().draft).not.toBeNull()
	})

	it("marks only the expired files so they can be attached again", async () => {
		vi.mocked(submitReport).mockRejectedValue(new SubmissionRejectedError("detail", ["gone"], []))
		const { result } = await renderForm()
		act(() => result.current.onAttachments("rev-files", () => [uploaded("gone"), uploaded("fine")]))

		await act(async () => result.current.onSubmit())

		expect(result.current.attachments["rev-files"].map((row) => row.status)).toEqual(["expired", "uploaded"])
		expect(result.current.submit).toMatchObject({ status: "failed", message: "report.attachments.expiredSummary" })
		expect(deleteUpload).not.toHaveBeenCalled()
	})

	it("marks refused files with their reason and erases them now", async () => {
		vi.mocked(submitReport).mockRejectedValue(
			new SubmissionRejectedError("detail", [], [{ uploadId: "bad", reason: "too_large" }]),
		)
		const { result } = await renderForm()
		act(() => result.current.onAttachments("rev-files", () => [uploaded("bad"), uploaded("fine")]))

		await act(async () => result.current.onSubmit())

		expect(result.current.attachments["rev-files"][0]).toMatchObject({ status: "rejected", reason: "too_large" })
		expect(result.current.attachments["rev-files"][1].status).toBe("uploaded")
		expect(result.current.submit).toMatchObject({ status: "failed", message: "report.attachments.refusedSummary" })
		expect(deleteUpload).toHaveBeenCalledWith("bad")
	})

	it("shows the API's own words when it rejects the report", async () => {
		vi.mocked(submitReport).mockRejectedValue(new SubmissionRejectedError("The API says no"))
		const { result } = await renderForm()

		await act(async () => result.current.onSubmit())

		expect(result.current.submit).toEqual({ status: "failed", message: "The API says no", keepsLocalState: true })
	})

	it("says the report could not be sent for any other failure", async () => {
		vi.mocked(submitReport).mockRejectedValue(new Error("boom"))
		const { result } = await renderForm()

		await act(async () => result.current.onSubmit())

		expect(result.current.submit).toEqual({ status: "failed", message: "report.error.submitFailed", keepsLocalState: true })
	})
})

describe("discarding the report", () => {
	it("asks first, and keeping the report closes the question", async () => {
		const { result } = await renderForm()
		expect(result.current.hasSomethingToDiscard).toBe(false)
		act(() => result.current.onAnswer("rev-name", text("Ada")))
		expect(result.current.hasSomethingToDiscard).toBe(true)

		act(() => result.current.onOpenDiscard())
		expect(result.current.confirmingDiscard).toBe(true)
		act(() => result.current.onKeepReport())

		expect(result.current.confirmingDiscard).toBe(false)
		expect(result.current.answers).toEqual({ "rev-name": text("Ada") })
	})

	it("offers discarding for a file row or an upload in flight alone", async () => {
		const { result } = await renderForm()

		act(() => result.current.onUploading("rev-files", true))
		expect(result.current.hasSomethingToDiscard).toBe(true)
		act(() => result.current.onUploading("rev-files", false))

		act(() => result.current.onAttachments("rev-files", () => [uploaded("u1")]))
		expect(result.current.hasSomethingToDiscard).toBe(true)
	})

	it("erases the answers, the finished files, and the saved report, and returns to the introduction", async () => {
		const { result } = await renderForm()
		act(() => result.current.onAnswer("rev-name", text("Ada")))
		act(() => result.current.onAttachments("rev-files", () => [uploaded("u1"), { ...uploaded("u2"), status: "rejected" }]))
		act(() => result.current.onUploading("rev-files", true))
		act(() => result.current.onOpenDiscard())
		await go("/report/name")

		act(() => result.current.onDiscard())

		expect(result.current.answers).toEqual({})
		expect(result.current.attachments).toEqual({})
		expect(result.current.anyUploading).toBe(false)
		expect(result.current.confirmingDiscard).toBe(false)
		expect(result.current.submit).toEqual({ status: "idle" })
		expect(deleteUpload).toHaveBeenCalledTimes(1)
		expect(deleteUpload).toHaveBeenCalledWith("u1")
		expect(readDraft().draft).toBeNull()
		await waitFor(() => expect(probe.path).toBe("/report"))
	})
})

describe("ReportForm", () => {
	it("words the not-tracked notice for the view", async () => {
		const { result } = await renderForm()

		expect(result.current.privacyNotice).toBe("report.privacy.localStorage")
	})

	it("renders the view with the view model", async () => {
		render(<ReportForm />, { wrapper: routed("/report") })

		expect(screen.getByTestId("view").getAttribute("data-status")).toBe("loading")
		await waitFor(() => expect(screen.getByTestId("view").getAttribute("data-status")).toBe("ready"))
	})
})
