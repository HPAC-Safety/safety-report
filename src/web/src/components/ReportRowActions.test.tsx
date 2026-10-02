import { fireEvent, render, renderHook, screen, cleanup } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"
import type { ReportListItem } from "../api/adminReports"
import { ReportRowActions, rowActionsFor, useReportRowActions } from "./ReportRowActions"
import type { ReactNode } from "react"
import { LocaleContext, type LocaleContextValue } from "../i18n/LocaleProvider"

afterEach(cleanup)

const value: LocaleContextValue = {
	locale: "en-CA",
	setLocale: () => {},
	t: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key),
}

function wrapper({ children }: { children: ReactNode }) {
	return <LocaleContext.Provider value={value}>{children}</LocaleContext.Provider>
}

const report = { status: "pending", consent: true } as ReportListItem

describe("useReportRowActions", () => {
	it("offers the actions the report's row allows", () => {
		const { result } = renderHook(() => useReportRowActions({ report, label: "L", busy: false, onAction: vi.fn() }))
		expect(result.current.actions).toEqual(["publish", "delete"])
		expect(rowActionsFor(report)).toEqual(["publish", "delete"])
	})
})

describe("ReportRowActions", () => {
	it("renders one named icon button per action and reports the one pressed", () => {
		const onAction = vi.fn()
		render(<ReportRowActions report={report} label="Row" busy={false} onAction={onAction} />, { wrapper })
		expect(screen.getByRole("group", { name: "Row" })).toBeTruthy()
		fireEvent.click(screen.getByRole("button", { name: "reports.action.delete" }))
		expect(onAction).toHaveBeenCalledWith("delete")
		fireEvent.click(screen.getByRole("button", { name: "reports.action.publish" }))
		expect(onAction).toHaveBeenCalledWith("publish")
	})

	it("disables every button while busy and offers Unpublish on a published report", () => {
		render(<ReportRowActions report={{ ...report, status: "published" }} label="Row" busy={true} onAction={vi.fn()} />, { wrapper })
		expect((screen.getByRole("button", { name: "reports.action.unpublish" }) as HTMLButtonElement).disabled).toBe(true)
	})
})
