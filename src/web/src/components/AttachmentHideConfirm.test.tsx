import { act, cleanup, fireEvent, render, renderHook, screen } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { LocaleContext } from "../i18n/LocaleProvider"
import { AttachmentHideConfirm, useAttachmentHideConfirm } from "./AttachmentHideConfirm"

afterEach(cleanup)

function wrapper({ children }: { children: ReactNode }) {
	return <LocaleContext.Provider value={{ locale: "en-CA", setLocale: () => {}, t: (key) => key }}>{children}</LocaleContext.Provider>
}

describe("useAttachmentHideConfirm", () => {
	it("asks, then hides and closes on confirm", () => {
		const onHide = vi.fn()
		const { result } = renderHook(() => useAttachmentHideConfirm({ onHide }))
		expect(result.current.confirming).toBe(false)
		act(() => result.current.onAsk())
		expect(result.current.confirming).toBe(true)
		act(() => result.current.onConfirm())
		expect(onHide).toHaveBeenCalledTimes(1)
		expect(result.current.confirming).toBe(false)
	})

	it("keeps the file when the reviewer backs out", () => {
		const onHide = vi.fn()
		const { result } = renderHook(() => useAttachmentHideConfirm({ onHide }))
		act(() => result.current.onAsk())
		act(() => result.current.onKeep())
		expect(result.current.confirming).toBe(false)
		expect(onHide).not.toHaveBeenCalled()
	})
})

describe("AttachmentHideConfirm", () => {
	it("walks from the Hide button through the question to the confirm and keep buttons", () => {
		const onHide = vi.fn()
		render(<AttachmentHideConfirm onHide={onHide} />, { wrapper })
		fireEvent.click(screen.getByRole("button", { name: "reports.attachment.hide" }))
		expect(screen.getByText("media.confirmHide")).toBeTruthy()
		fireEvent.click(screen.getByRole("button", { name: "media.keep" }))
		expect(screen.getByRole("button", { name: "reports.attachment.hide" })).toBeTruthy()
		fireEvent.click(screen.getByRole("button", { name: "reports.attachment.hide" }))
		fireEvent.click(screen.getByRole("button", { name: "media.hide" }))
		expect(onHide).toHaveBeenCalledTimes(1)
	})
})
