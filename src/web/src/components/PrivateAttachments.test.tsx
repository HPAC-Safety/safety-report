import { act, cleanup, fireEvent, render, renderHook, screen, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { listPrivateAttachments, privateAttachmentLink, removePrivateAttachment, type PrivateAttachment } from "../api/adminReports"
import { LocaleContext } from "../i18n/LocaleProvider"
import { downloadPrivateAttachment, PrivateAttachments, usePrivateAttachments } from "./PrivateAttachments"

vi.mock("../api/adminReports", async (importOriginal) => ({
	...(await importOriginal<typeof import("../api/adminReports")>()),
	listPrivateAttachments: vi.fn(),
	privateAttachmentLink: vi.fn(),
	removePrivateAttachment: vi.fn(),
}))
vi.mock("./PrivateAttachmentStaging", () => ({
	PrivateAttachmentStaging: (props: { reportId: string; onAdded: () => void }) => (
		<button data-staging={props.reportId} onClick={props.onAdded}>
			staging
		</button>
	),
}))

beforeEach(() => {
	vi.mocked(listPrivateAttachments).mockReset()
	vi.mocked(privateAttachmentLink).mockReset().mockResolvedValue({ url: "https://files/x", expiresAt: "", fileName: "x.pdf" })
	vi.mocked(removePrivateAttachment).mockReset().mockResolvedValue(undefined)
})
afterEach(cleanup)

function wrapper({ children }: { children: ReactNode }) {
	return <LocaleContext.Provider value={{ locale: "en-CA", setLocale: () => {}, t: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key) }}>{children}</LocaleContext.Provider>
}

const attachment = (over: Partial<PrivateAttachment> = {}): PrivateAttachment => ({
	id: "a1",
	fileName: "plan.pdf",
	contentType: "application/pdf",
	byteSize: 2048,
	description: null,
	addedBy: "sub-1",
	addedAt: "2026-01-02T03:04:00Z",
	isMine: false,
	...over,
})

describe("usePrivateAttachments", () => {
	it("loads the list", async () => {
		vi.mocked(listPrivateAttachments).mockResolvedValue([attachment()])
		const { result } = renderHook(() => usePrivateAttachments("r1"))
		expect(result.current.attachments).toBeNull()
		await waitFor(() => expect(result.current.attachments).toHaveLength(1))
		expect(result.current.failed).toBe(false)
	})

	it("treats anything but a list as a failed read, and a rejected read too", async () => {
		vi.mocked(listPrivateAttachments).mockResolvedValue({} as unknown as PrivateAttachment[])
		const { result } = renderHook(() => usePrivateAttachments("r1"))
		await waitFor(() => expect(result.current.failed).toBe(true))
		vi.mocked(listPrivateAttachments).mockRejectedValue(new Error("no"))
		act(() => result.current.reload())
		await waitFor(() => expect(listPrivateAttachments).toHaveBeenCalledTimes(2))
		expect(result.current.failed).toBe(true)
	})

	it("clears a failure once a read succeeds", async () => {
		vi.mocked(listPrivateAttachments).mockRejectedValueOnce(new Error("no")).mockResolvedValue([])
		const { result } = renderHook(() => usePrivateAttachments("r1"))
		await waitFor(() => expect(result.current.failed).toBe(true))
		act(() => result.current.reload())
		await waitFor(() => expect(result.current.failed).toBe(false))
	})
})

describe("downloadPrivateAttachment", () => {
	it("clicks a temporary anchor named for the file and removes it", async () => {
		const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {})
		await downloadPrivateAttachment("r1", "a1")
		expect(privateAttachmentLink).toHaveBeenCalledWith("r1", "a1")
		expect(click).toHaveBeenCalled()
		expect(document.querySelector("a[download]")).toBeNull()
		click.mockRestore()
	})
})

describe("PrivateAttachments", () => {
	const state = (over: Partial<Parameters<typeof PrivateAttachments>[0]["state"]> = {}) => ({ attachments: [attachment()], failed: false, reload: vi.fn(), ...over })

	it("shows a failed read", () => {
		render(<PrivateAttachments reportId="r1" state={state({ failed: true })} />, { wrapper })
		expect(screen.getByText("privateAttachments.error.load")).toBeTruthy()
	})

	it("shows loading, then empty", () => {
		render(<PrivateAttachments reportId="r1" state={state({ attachments: null })} />, { wrapper })
		expect(screen.getByText("privateAttachments.loading")).toBeTruthy()
		cleanup()
		render(<PrivateAttachments reportId="r1" state={state({ attachments: [] })} />, { wrapper })
		expect(screen.getByText("privateAttachments.empty")).toBeTruthy()
	})

	it("lists attachments with size and date in the reader's language and reloads when staging adds", () => {
		const current = state()
		render(<PrivateAttachments reportId="r1" state={current} />, { wrapper })
		expect(screen.getByText("plan.pdf")).toBeTruthy()
		expect(screen.getByText("2 kB")).toBeTruthy()
		expect(document.querySelector("[data-private-attachment-added]")?.textContent).toMatch(/privateAttachments\.added/)
		fireEvent.click(screen.getByText("staging"))
		expect(current.reload).toHaveBeenCalled()
	})

	it("downloads one, and shows an error when it fails", async () => {
		const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {})
		render(<PrivateAttachments reportId="r1" state={state()} />, { wrapper })
		fireEvent.click(screen.getByRole("button", { name: "privateAttachments.download" }))
		await waitFor(() => expect(click).toHaveBeenCalled())
		expect(screen.queryByRole("alert")).toBeNull()
		vi.mocked(privateAttachmentLink).mockRejectedValueOnce(new Error("no"))
		fireEvent.click(screen.getByRole("button", { name: "privateAttachments.download" }))
		expect((await screen.findByRole("alert")).textContent).toBe("privateAttachments.error.download")
		click.mockRestore()
	})

	it("removes one after a confirmation and reloads, even when it fails", async () => {
		const current = state()
		render(<PrivateAttachments reportId="r1" state={current} />, { wrapper })
		fireEvent.click(screen.getByRole("button", { name: "privateAttachments.remove" }))
		fireEvent.click(screen.getByRole("button", { name: "privateAttachments.remove" }))
		await waitFor(() => expect(current.reload).toHaveBeenCalledTimes(1))
		expect(removePrivateAttachment).toHaveBeenCalledWith("r1", "a1")
		vi.mocked(removePrivateAttachment).mockRejectedValueOnce(new Error("no"))
		fireEvent.click(screen.getByRole("button", { name: "privateAttachments.remove" }))
		fireEvent.click(screen.getByRole("button", { name: "privateAttachments.remove" }))
		expect((await screen.findByRole("alert")).textContent).toBe("privateAttachments.error.remove")
		expect(current.reload).toHaveBeenCalledTimes(2)
	})
})
