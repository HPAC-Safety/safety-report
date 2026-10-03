import { act, render, renderHook, screen, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom"
import { beforeEach, describe, expect, it, vi } from "vitest"

const api = vi.hoisted(() => ({
	deleteReport: vi.fn(),
	getReport: vi.fn(),
	publishReport: vi.fn(),
	rollBackSummary: vi.fn(),
	saveSummaryPair: vi.fn(),
	unpublishReport: vi.fn(),
}))
const privateAttachments = vi.hoisted(() => ({ state: { attachments: [], failed: false, reload: vi.fn() }, usePrivateAttachments: vi.fn() }))
const t = vi.hoisted(() => (key: string) => key)
vi.mock("../i18n/useLocale", () => ({ useLocale: () => ({ locale: "en-CA", t }) }))
vi.mock("../api/adminReports", async (original) => ({ ...(await original<typeof import("../api/adminReports")>()), ...api }))
vi.mock("../api/adminQuestions", () => ({
	ApiError: class ApiError extends Error {
		constructor(
			public status: number,
			public type: string,
			public detail: string,
		) {
			super(detail)
		}
	},
}))
vi.mock("../components/PrivateAttachments", () => ({ usePrivateAttachments: privateAttachments.usePrivateAttachments }))
vi.mock("./ReportDetailPage.view", () => ({
	ReportDetailPageView: (props: { report: { id: string } | null }) => <p data-testid="view">{props.report?.id ?? "none"}</p>,
}))

import { ApiError } from "../api/adminQuestions"
import { STALE_REPORT } from "../api/adminReports"
import { ReportDetailPage, useReportDetailPage, type ReportAnswer, type ReportDetail } from "./ReportDetailPage"

let path = ""
function Probe() {
	path = useLocation().pathname
	return null
}
const wrapper = ({ children }: { children: ReactNode }) => (
	<MemoryRouter initialEntries={["/admin/reports/r1"]}>
		<Probe />
		<Routes>
			<Route path="/admin/reports/:reportId" element={<>{children}</>} />
			<Route path="/admin/reports" element={null} />
		</Routes>
	</MemoryRouter>
)
const answer = (key: string, isPrivate: boolean) => ({ questionKey: key, isPrivate }) as ReportAnswer
const report = (over: Record<string, unknown> = {}) => ({ id: "r1", version: "v1", answers: [answer("a", false), answer("b", true)], ...over }) as unknown as ReportDetail
const failure = (status: number, type: string, detail: string) => new (ApiError as unknown as new (status: number, type: string, detail: string) => Error)(status, type, detail)
const loaded = async (over: Record<string, unknown> = {}) => {
	api.getReport.mockResolvedValue(report(over))
	const hook = renderHook(() => useReportDetailPage(), { wrapper })
	await waitFor(() => expect(hook.result.current.report).not.toBeNull())
	return hook
}

describe("useReportDetailPage", () => {
	beforeEach(() => {
		for (const fn of Object.values(api)) fn.mockReset()
		privateAttachments.usePrivateAttachments.mockReset().mockReturnValue(privateAttachments.state)
		path = ""
	})

	it("loads the report named in the address and shares the private attachments of it", async () => {
		const { result } = await loaded()

		expect(api.getReport).toHaveBeenCalledWith("r1")
		expect(privateAttachments.usePrivateAttachments).toHaveBeenCalledWith("r1")
		expect(result.current.privateAttachments).toBe(privateAttachments.state)
		expect(result.current.error).toBeNull()
	})

	it("splits the answers into ordinary and private ones", async () => {
		const { result } = await loaded()

		expect(result.current.ordinaryAnswers.map((item) => item.questionKey)).toEqual(["a"])
		expect(result.current.privateAnswers.map((item) => item.questionKey)).toEqual(["b"])
	})

	it("has no answers before the report loads", () => {
		api.getReport.mockReturnValue(new Promise(() => {}))
		const { result } = renderHook(() => useReportDetailPage(), { wrapper })

		expect(result.current).toMatchObject({ report: null, ordinaryAnswers: [], privateAnswers: [] })
	})

	it("names the consent word for each answer given", async () => {
		const { result } = await loaded()

		expect([true, false, null].map(result.current.consentKey)).toEqual(["yes", "no", "unanswered"])
	})

	it("says the report was not found, or why it could not load", async () => {
		api.getReport.mockRejectedValueOnce(failure(404, "x", "gone"))
		expect(await errorOf()).toBe("reports.detail.notFound")
		api.getReport.mockRejectedValueOnce(failure(500, "x", "detail"))
		expect(await errorOf()).toBe("detail")
		api.getReport.mockRejectedValueOnce(new Error("no"))
		expect(await errorOf()).toBe("reports.error.unexpected")

		async function errorOf() {
			const { result } = renderHook(() => useReportDetailPage(), { wrapper })
			await waitFor(() => expect(result.current.error).not.toBeNull())
			return result.current.error
		}
	})

	it("ignores a load that finishes after the page was left", async () => {
		let finish: (value: unknown) => void = () => {}
		let refuse: (reason: unknown) => void = () => {}
		api.getReport.mockReturnValueOnce(new Promise((resolve) => (finish = resolve))).mockReturnValueOnce(new Promise((_, reject) => (refuse = reject)))
		const first = renderHook(() => useReportDetailPage(), { wrapper })
		const second = renderHook(() => useReportDetailPage(), { wrapper })
		first.unmount()
		second.unmount()

		await act(async () => {
			finish(report())
			refuse(new Error("no"))
		})

		expect(first.result.current.report).toBeNull()
		expect(second.result.current.error).toBeNull()
	})

	it("sends each command the version the page loaded and shows the result", async () => {
		const { result } = await loaded()
		api.saveSummaryPair.mockResolvedValue(report({ version: "v2" }))
		api.unpublishReport.mockResolvedValue(report({ version: "v3" }))
		api.publishReport.mockResolvedValue(report({ version: "v4" }))
		api.rollBackSummary.mockResolvedValue(report({ version: "v5" }))

		await act(async () => {
			expect(await result.current.save("en", "fr", "human", "machine")).toBe(true)
		})
		expect(api.saveSummaryPair).toHaveBeenCalledWith("r1", "v1", "en", "fr", "human", "machine")
		expect(result.current.report?.version).toBe("v2")

		await act(async () => await result.current.unpublish("why"))
		expect(api.unpublishReport).toHaveBeenCalledWith("r1", "v2", "why")

		await act(async () => result.current.publish())
		expect(api.publishReport).toHaveBeenCalledWith("r1", "v3")

		await act(async () => await result.current.restore("rev1"))
		expect(api.rollBackSummary).toHaveBeenCalledWith("r1", "v4", "rev1")
		expect(result.current.report?.version).toBe("v5")
		expect(result.current.busy).toBe(false)
	})

	it("is busy while a command runs", async () => {
		const { result } = await loaded()
		let finish: (value: unknown) => void = () => {}
		api.publishReport.mockReturnValue(new Promise((resolve) => (finish = resolve)))

		act(() => result.current.publish())
		expect(result.current.busy).toBe(true)
		await act(async () => finish(report()))

		expect(result.current.busy).toBe(false)
	})

	it("refuses a command before the report has loaded", async () => {
		api.getReport.mockReturnValue(new Promise(() => {}))
		const { result } = renderHook(() => useReportDetailPage(), { wrapper })

		await act(async () => {
			expect(await result.current.unpublish("why")).toBe(false)
		})

		expect(api.unpublishReport).not.toHaveBeenCalled()
	})

	it("offers a reload when another reviewer changed the report first", async () => {
		const { result } = await loaded()
		api.unpublishReport.mockRejectedValue(failure(409, STALE_REPORT, "stale"))

		await act(async () => {
			expect(await result.current.unpublish("why")).toBe(false)
		})
		expect(result.current.stale).toBe(true)
		expect(result.current.error).toBeNull()

		act(() => result.current.reload())
		await waitFor(() => expect(api.getReport).toHaveBeenCalledTimes(2))
		expect(result.current.stale).toBe(false)
	})

	it("shows the API's reason for a refused command, else a generic one", async () => {
		const { result } = await loaded()
		api.unpublishReport.mockRejectedValueOnce(failure(500, "other", "detail")).mockRejectedValueOnce(new Error("no"))

		await act(async () => await result.current.unpublish("why"))
		expect(result.current.error).toBe("detail")
		await act(async () => await result.current.unpublish("why"))
		expect(result.current.error).toBe("reports.error.unexpected")
	})

	it("asks before deleting, then deletes and returns to the list", async () => {
		api.deleteReport.mockResolvedValue(undefined)
		const { result } = await loaded()

		act(() => result.current.askDelete())
		expect(result.current.confirmingDelete).toBe(true)
		act(() => result.current.keepReport())
		expect(result.current.confirmingDelete).toBe(false)

		act(() => result.current.askDelete())
		await act(async () => result.current.remove())

		expect(api.deleteReport).toHaveBeenCalledWith("r1")
		expect(path).toBe("/admin/reports")
	})

	it("stays on the report and says why when the delete is refused", async () => {
		const { result } = await loaded()
		api.deleteReport.mockRejectedValueOnce(failure(500, "other", "detail")).mockRejectedValueOnce(new Error("no"))

		await act(async () => result.current.remove())
		expect(result.current.error).toBe("detail")
		expect(result.current.busy).toBe(false)
		await act(async () => result.current.remove())

		expect(result.current.error).toBe("reports.error.unexpected")
		expect(path).toBe("/admin/reports/r1")
	})
})

describe("ReportDetailPage", () => {
	it("renders its view with the view model", async () => {
		api.getReport.mockResolvedValue(report())
		render(<ReportDetailPage />, { wrapper })

		await waitFor(() => expect(screen.getByTestId("view").textContent).toBe("r1"))
	})
})
