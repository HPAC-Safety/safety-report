import { act, fireEvent, render, renderHook, screen, cleanup } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"
import type { SummaryRevision } from "./SummaryHistory"
import { SummaryHistory, useSummaryHistory } from "./SummaryHistory"

vi.mock("./RestoreVersionDialog", () => ({
	RestoreVersionDialog: (props: { sequence: number; onConfirm: () => void; onKeep: () => void }) => (
		<div data-dialog={props.sequence}>
			<button onClick={props.onConfirm}>confirm</button>
			<button onClick={props.onKeep}>keep</button>
		</div>
	),
}))
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

const revision = (overrides: Partial<SummaryRevision>): SummaryRevision => ({
	id: "a",
	sequence: 2,
	aiSummaryEn: "English text",
	aiSummaryFr: "Texte français",
	sourceEn: "human",
	sourceFr: "machine",
	authorSubject: "someone",
	createdAt: "2026-01-02T03:04:00Z",
	restoredFromSequence: null,
	approvedBySubject: null,
	approvedAt: null,
	isCurrent: false,
	...overrides,
})

const props = (onRestore = vi.fn().mockResolvedValue(true)) => ({
	revisions: [revision({}), revision({ id: "b", sequence: 1, isCurrent: true })],
	isLive: false,
	canRestore: true,
	busy: false,
	onRestore,
})

describe("useSummaryHistory", () => {
	it("opens and closes one version at a time", () => {
		const { result } = renderHook(() => useSummaryHistory(props()), { wrapper })
		expect(result.current.viewing).toBeNull()
		act(() => result.current.onToggleView("a"))
		expect(result.current.viewing).toBe("a")
		act(() => result.current.onToggleView("a"))
		expect(result.current.viewing).toBeNull()
	})

	it("formats a timestamp in the locale", () => {
		const { result } = renderHook(() => useSummaryHistory(props()), { wrapper })
		expect(result.current.formatAt("2026-01-02T03:04:00Z")).toContain("2026")
	})

	it("names the author, the Worker, or an unknown one", () => {
		const { result } = renderHook(() => useSummaryHistory(props()), { wrapper })
		expect(result.current.author(revision({ authorSubject: "sub" }))).toBe("sub")
		expect(result.current.author(revision({ authorSubject: null, sequence: 1, sourceEn: "generated", sourceFr: "generated" }))).toBe("reports.history.author.worker")
		expect(result.current.author(revision({ authorSubject: null, sequence: 1, sourceEn: "generated", sourceFr: "machine" }))).toBe("reports.history.author.unknown")
		expect(result.current.author(revision({ authorSubject: null, sequence: 2, sourceEn: "generated", sourceFr: "generated" }))).toBe("reports.history.author.unknown")
	})

	it("restores the asked-about revision on confirm, and does nothing on a confirm with nothing asked", async () => {
		const onRestore = vi.fn().mockResolvedValue(true)
		const { result } = renderHook(() => useSummaryHistory(props(onRestore)), { wrapper })
		await act(async () => {
			result.current.onConfirm()
			await Promise.resolve()
		})
		expect(onRestore).not.toHaveBeenCalled()
		act(() => result.current.onAskRestore(revision({ id: "a" })))
		expect(result.current.restoring?.id).toBe("a")
		await act(async () => {
			result.current.onConfirm()
			await Promise.resolve()
		})
		expect(onRestore).toHaveBeenCalledWith("a")
		expect(result.current.restoring).toBeNull()
	})

	it("keeps the current version when asked to", () => {
		const { result } = renderHook(() => useSummaryHistory(props()), { wrapper })
		act(() => result.current.onAskRestore(revision({})))
		act(() => result.current.onKeep())
		expect(result.current.restoring).toBeNull()
	})
})

describe("SummaryHistory", () => {
	it("lists versions, views one, and restores behind a confirmation", async () => {
		const onRestore = vi.fn().mockResolvedValue(true)
		const { container } = render(<SummaryHistory {...props(onRestore)} />, { wrapper })
		expect(container.querySelectorAll("[data-revision]")).toHaveLength(2)
		fireEvent.click(screen.getAllByRole("button", { name: "reports.history.view" })[0])
		expect(container.querySelector("[data-revision-text='en']")?.textContent).toContain("English text")
		fireEvent.click(screen.getByRole("button", { name: "reports.history.restore" }))
		expect(container.querySelector("[data-dialog='2']")).not.toBeNull()
		await act(() => fireEvent.click(screen.getByText("confirm")))
		expect(onRestore).toHaveBeenCalledWith("a")
		expect(container.querySelector("[data-dialog]")).toBeNull()
	})
})
