import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"
import type { PrivateAttachment } from "../api/adminReports"
import { LocaleContext } from "../i18n/LocaleProvider"
import { PrivateAttachmentItem } from "./PrivateAttachmentItem"

afterEach(cleanup)

function wrapper({ children }: { children: ReactNode }) {
	return <LocaleContext.Provider value={{ locale: "en-CA", setLocale: () => {}, t: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key) }}>{children}</LocaleContext.Provider>
}

const attachment = (over: Partial<PrivateAttachment> = {}): PrivateAttachment => ({
	id: "a1",
	fileName: "plan.pdf",
	contentType: "application/pdf",
	byteSize: 1,
	description: null,
	addedBy: "sub-1",
	addedAt: "",
	isMine: false,
	...over,
})

function mount(over: Partial<PrivateAttachment> = {}, onRemove = vi.fn().mockResolvedValue(undefined)) {
	const onDownload = vi.fn()
	render(
		<ol>
			<PrivateAttachmentItem attachment={attachment(over)} size="2 kB" addedAt="Jan 2" onDownload={onDownload} onRemove={onRemove} />
		</ol>,
		{ wrapper },
	)
	return { onDownload, onRemove }
}

describe("PrivateAttachmentItem", () => {
	it("shows the file, its size, a description when there is one, and who added it", () => {
		mount({ description: "Site plan" })
		expect(screen.getByText("plan.pdf")).toBeTruthy()
		expect(screen.getByText("2 kB")).toBeTruthy()
		expect(screen.getByText("Site plan")).toBeTruthy()
		expect(document.querySelector("[data-private-attachment-added]")?.textContent).toBe('privateAttachments.added {"adder":"sub-1","at":"Jan 2"}')
	})

	it("names you as the adder of your own file and omits an empty description", () => {
		mount({ isMine: true })
		expect(document.querySelector("[data-private-attachment-added]")?.textContent).toContain("privateAttachments.author.you")
		expect(document.querySelector("[data-private-attachment-description]")).toBeNull()
	})

	it("downloads", () => {
		const { onDownload } = mount()
		fireEvent.click(screen.getByRole("button", { name: "privateAttachments.download" }))
		expect(onDownload).toHaveBeenCalled()
	})

	it("asks before removing, and keeps the file when backed out", () => {
		const { onRemove } = mount()
		fireEvent.click(screen.getByRole("button", { name: "privateAttachments.remove" }))
		expect(screen.getByText("privateAttachments.confirmRemove")).toBeTruthy()
		fireEvent.click(screen.getByRole("button", { name: "privateAttachments.keep" }))
		expect(screen.getByRole("button", { name: "privateAttachments.download" })).toBeTruthy()
		expect(onRemove).not.toHaveBeenCalled()
	})

	it("removes on confirmation and closes the question once done", async () => {
		const { onRemove } = mount()
		fireEvent.click(screen.getByRole("button", { name: "privateAttachments.remove" }))
		fireEvent.click(screen.getByRole("button", { name: "privateAttachments.remove" }))
		expect(onRemove).toHaveBeenCalled()
		await waitFor(() => expect(screen.getByRole("button", { name: "privateAttachments.download" })).toBeTruthy())
	})
})
