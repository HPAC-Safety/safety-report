import { act, cleanup, fireEvent, render, renderHook, screen } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { LocaleContext } from "../i18n/LocaleProvider"
import { AttachmentLightbox, useAttachmentLightbox, type AttachmentLightboxProps } from "./AttachmentLightbox"
import type { StripItem } from "./stripItems"
import { present } from "../lib/present"

vi.mock("./AttachmentLightboxMedia", () => ({
	AttachmentLightboxMedia: (props: { item: StripItem; onGone: () => void }) => (
		<button data-media={props.item.id} onClick={props.onGone}>
			media
		</button>
	),
}))

const showModal = vi.fn(function (this: HTMLDialogElement) {
	this.setAttribute("open", "")
})
const close = vi.fn(function (this: HTMLDialogElement) {
	this.removeAttribute("open")
})

beforeEach(() => {
	showModal.mockClear()
	close.mockClear()
	HTMLDialogElement.prototype.showModal = showModal
	HTMLDialogElement.prototype.close = close
})
afterEach(cleanup)

function wrapper({ children }: { children: ReactNode }) {
	return <LocaleContext.Provider value={{ locale: "en-CA", setLocale: () => {}, t: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key) }}>{children}</LocaleContext.Provider>
}

const item = (id: string, kind: StripItem["kind"] = "image"): StripItem => ({ id, kind, format: null, state: "ready", visibility: null })
const items = [item("a"), item("b", "video"), item("c")]

function props(over: Partial<AttachmentLightboxProps> = {}): AttachmentLightboxProps {
	return { items, openId: "a", getLink: vi.fn(), invalidateLink: vi.fn(), onClose: vi.fn(), onGone: vi.fn(), ...over }
}

describe("AttachmentLightbox", () => {
	it("opens as a modal at the requested item with a generic label", () => {
		render(<AttachmentLightbox {...props({ openId: "c" })} />, { wrapper })
		expect(showModal).toHaveBeenCalledTimes(1)
		expect(screen.getByTestId("attachment-lightbox").getAttribute("aria-label")).toBe('media.photoLabel {"index":2,"count":2}')
		expect(document.querySelector("[data-media]")?.getAttribute("data-media")).toBe("c")
	})

	it("starts at the first item when the requested one is not there", () => {
		render(<AttachmentLightbox {...props({ openId: "zzz" })} />, { wrapper })
		expect(document.querySelector("[data-media]")?.getAttribute("data-media")).toBe("a")
	})

	it("labels a video by its position among videos", () => {
		render(<AttachmentLightbox {...props({ openId: "b" })} />, { wrapper })
		expect(screen.getByTestId("attachment-lightbox").getAttribute("aria-label")).toBe('media.videoLabel {"index":1,"count":1}')
	})

	it("does not open a dialog that is already open", () => {
		const open = present(Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, "open"))
		Object.defineProperty(HTMLDialogElement.prototype, "open", { get: () => true, configurable: true })
		try {
			render(<AttachmentLightbox {...props()} />, { wrapper })
			expect(showModal).not.toHaveBeenCalled()
		} finally {
			Object.defineProperty(HTMLDialogElement.prototype, "open", open)
		}
	})

	it("steps with the carets and the arrow keys, wrapping at both ends", () => {
		render(<AttachmentLightbox {...props()} />, { wrapper })
		const media = () => document.querySelector("[data-media]")?.getAttribute("data-media")
		fireEvent.click(screen.getByRole("button", { name: "media.lightbox.previous" }))
		expect(media()).toBe("c")
		fireEvent.click(screen.getByRole("button", { name: "media.lightbox.next" }))
		expect(media()).toBe("a")
		fireEvent.keyDown(screen.getByTestId("attachment-lightbox"), { key: "ArrowRight" })
		expect(media()).toBe("b")
		fireEvent.keyDown(screen.getByTestId("attachment-lightbox"), { key: "ArrowLeft" })
		expect(media()).toBe("a")
		fireEvent.keyDown(screen.getByTestId("attachment-lightbox"), { key: "Tab" })
		expect(media()).toBe("a")
	})

	it("closes the native dialog before telling the parent, from the button and from Escape", () => {
		const onClose = vi.fn()
		render(<AttachmentLightbox {...props({ onClose })} />, { wrapper })
		fireEvent.click(screen.getByRole("button", { name: "media.lightbox.close" }))
		expect(close).toHaveBeenCalledTimes(1)
		expect(onClose).toHaveBeenCalledTimes(1)
		const cancel = new Event("cancel", { cancelable: true })
		fireEvent(screen.getByTestId("attachment-lightbox"), cancel)
		expect(cancel.defaultPrevented).toBe(true)
		expect(onClose).toHaveBeenCalledTimes(2)
	})

	it("tells the parent which item has gone", () => {
		const onGone = vi.fn()
		render(<AttachmentLightbox {...props({ onGone })} />, { wrapper })
		fireEvent.click(screen.getByText("media"))
		expect(onGone).toHaveBeenCalledWith("a")
	})

	it("clamps to the last item when the open one is removed", () => {
		const { rerender } = render(<AttachmentLightbox {...props({ openId: "c" })} />, { wrapper })
		rerender(<AttachmentLightbox {...props({ openId: "c", items: [item("a")] })} />)
		expect(document.querySelector("[data-media]")?.getAttribute("data-media")).toBe("a")
	})

	it("closes itself when no item is left", () => {
		const onClose = vi.fn()
		const { rerender } = render(<AttachmentLightbox {...props({ onClose })} />, { wrapper })
		rerender(<AttachmentLightbox {...props({ onClose, items: [] })} />)
		expect(onClose).toHaveBeenCalled()
		expect(screen.queryByTestId("attachment-lightbox")).toBeNull()
	})
})

describe("useAttachmentLightbox", () => {
	it("stays on the first position when there is nothing to step through", () => {
		const { result } = renderHook(() => useAttachmentLightbox(props({ items: [] })), { wrapper })
		act(() => result.current.next())
		expect(result.current.item).toBeUndefined()
		expect(result.current.label).toBe("")
		act(() => result.current.onItemGone())
	})
})
