import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "../api/adminQuestions"
import { LocaleContext } from "../i18n/LocaleProvider"
import { AttachmentThumbnail, type AttachmentThumbnailProps } from "./AttachmentThumbnail"
import type { StripItem } from "./stripItems"
import { present } from "../lib/present"

afterEach(cleanup)

function wrapper({ children }: { children: ReactNode }) {
	return <LocaleContext.Provider value={{ locale: "en-CA", setLocale: () => {}, t: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key) }}>{children}</LocaleContext.Provider>
}

const item = (over: Partial<StripItem> = {}): StripItem => ({ id: "i", kind: "image", format: null, state: "ready", visibility: null, ...over })

function mount(over: Partial<AttachmentThumbnailProps> = {}) {
	const props: AttachmentThumbnailProps = {
		item: item(),
		label: "Photo 1 of 1",
		staff: false,
		getLink: vi.fn().mockResolvedValue("https://files/t"),
		invalidateLink: vi.fn(),
		onActivate: vi.fn(),
		onGone: vi.fn(),
		onHide: null,
		onShow: null,
		...over,
	}
	render(<AttachmentThumbnail {...props} />, { wrapper })
	return props
}

describe("AttachmentThumbnail", () => {
	it("loads an image's derivative as its thumbnail", async () => {
		mount()
		await waitFor(() => expect(document.querySelector("img")?.getAttribute("src")).toBe("https://files/t"))
	})

	it("passes its button to onActivate", () => {
		const props = mount()
		const trigger = screen.getByRole("button", { name: "Photo 1 of 1" })
		fireEvent.click(trigger)
		expect(props.onActivate).toHaveBeenCalledWith(trigger)
	})

	it("gives a video a play tile and asks for no link", () => {
		const props = mount({ item: item({ kind: "video" }), label: "Video 1 of 1" })
		expect(document.querySelector("img")).toBeNull()
		expect(document.querySelector("svg path")?.getAttribute("d")).toBe("M8 5v14l11-7z")
		expect(props.getLink).not.toHaveBeenCalled()
	})

	it("gives a document a type icon and a download label", () => {
		mount({ item: item({ kind: "document", format: "pdf" }), label: "Document" })
		expect(screen.getByRole("button", { name: 'media.downloadLabel {"label":"Document"}' })).toBeTruthy()
		expect(screen.getByText("pdf")).toBeTruthy()
	})

	it("asks for no link while an image is still processing", () => {
		const props = mount({ item: item({ state: "processing" }) })
		expect(props.getLink).not.toHaveBeenCalled()
		expect(document.querySelector("img")).toBeNull()
	})

	it("tells the parent when the image has gone, and ignores other failures", async () => {
		const gone = mount({ getLink: vi.fn().mockRejectedValue(new ApiError(404, "gone")) })
		await waitFor(() => expect(gone.onGone).toHaveBeenCalled())
		cleanup()
		const other = mount({ getLink: vi.fn().mockRejectedValue(new Error("network")) })
		await waitFor(() => expect(other.getLink).toHaveBeenCalled())
		expect(other.onGone).not.toHaveBeenCalled()
	})

	it("retries once with a fresh link when the bytes fail, then leaves it blank", async () => {
		const props = mount()
		await waitFor(() => expect(document.querySelector("img")).toBeTruthy())
		fireEvent.error(present(document.querySelector("img")))
		expect(props.invalidateLink).toHaveBeenCalledWith("i")
		await waitFor(() => expect(props.getLink).toHaveBeenCalledTimes(2))
		fireEvent.error(present(document.querySelector("img")))
		expect(props.invalidateLink).toHaveBeenCalledTimes(1)
		expect(props.getLink).toHaveBeenCalledTimes(2)
	})

	it("shows staff the state or visibility", () => {
		mount({ staff: true, item: item({ state: "failed" }) })
		expect(screen.getByTestId("attachment-state").textContent).toBe("reports.attachment.state.failed")
		cleanup()
		mount({ staff: true, item: item({ visibility: "no_consent" }) })
		expect(screen.getByTestId("attachment-visibility").textContent).toBe("reports.attachment.visibility.no_consent")
		cleanup()
		mount({ staff: true })
		expect(screen.queryByTestId("attachment-visibility")).toBeNull()
		expect(screen.queryByTestId("attachment-state")).toBeNull()
	})

	it("offers Hide and Show only when given", () => {
		const onShow = vi.fn()
		mount({ onShow, onHide: vi.fn() })
		expect(screen.getByRole("button", { name: "reports.attachment.hide" })).toBeTruthy()
		fireEvent.click(screen.getByRole("button", { name: "reports.attachment.show" }))
		expect(onShow).toHaveBeenCalled()
	})
})
