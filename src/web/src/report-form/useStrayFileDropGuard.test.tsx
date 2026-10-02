import { cleanup, renderHook } from "@testing-library/react"
import { afterEach, describe, expect, it } from "vitest"
import { useStrayFileDropGuard } from "./useStrayFileDropGuard"

afterEach(cleanup)

function dragEvent(type: string, types: string[] | null, withTransfer = true): Event & { dataTransfer: unknown } {
	const event = new Event(type, { bubbles: true, cancelable: true }) as Event & { dataTransfer: unknown }
	event.dataTransfer = withTransfer ? { types, dropEffect: "copy" } : null
	return event
}

describe("useStrayFileDropGuard", () => {
	it("prevents a stray file drop and sets the drop effect to none", () => {
		renderHook(() => useStrayFileDropGuard())
		const event = dragEvent("drop", ["Files"])
		window.dispatchEvent(event)
		expect(event.defaultPrevented).toBe(true)
		expect((event.dataTransfer as { dropEffect: string }).dropEffect).toBe("none")
	})

	it("prevents a file dragover too", () => {
		renderHook(() => useStrayFileDropGuard())
		const event = dragEvent("dragover", ["Files"])
		window.dispatchEvent(event)
		expect(event.defaultPrevented).toBe(true)
	})

	it("ignores a drag that carries no files", () => {
		renderHook(() => useStrayFileDropGuard())
		const event = dragEvent("drop", ["text/plain"])
		window.dispatchEvent(event)
		expect(event.defaultPrevented).toBe(false)
	})

	it("ignores an event already handled by a drop zone", () => {
		renderHook(() => useStrayFileDropGuard())
		const event = dragEvent("drop", ["Files"])
		event.preventDefault()
		window.dispatchEvent(event)
		expect((event.dataTransfer as { dropEffect: string }).dropEffect).toBe("copy")
	})

	it("ignores an event with no data transfer", () => {
		renderHook(() => useStrayFileDropGuard())
		const event = dragEvent("drop", null, false)
		window.dispatchEvent(event)
		expect(event.defaultPrevented).toBe(false)
	})

	it("tolerates a transfer whose types are missing", () => {
		renderHook(() => useStrayFileDropGuard())
		const event = dragEvent("drop", null)
		window.dispatchEvent(event)
		expect(event.defaultPrevented).toBe(false)
	})

	it("still prevents the drop when the transfer disappears after the file check", () => {
		renderHook(() => useStrayFileDropGuard())
		const event = new Event("drop", { bubbles: true, cancelable: true })
		let reads = 0
		Object.defineProperty(event, "dataTransfer", {
			get: () => (reads++ === 0 ? { types: ["Files"], dropEffect: "copy" } : null),
		})
		window.dispatchEvent(event)
		expect(event.defaultPrevented).toBe(true)
	})

	it("stops listening on unmount", () => {
		const { unmount } = renderHook(() => useStrayFileDropGuard())
		unmount()
		const event = dragEvent("drop", ["Files"])
		window.dispatchEvent(event)
		expect(event.defaultPrevented).toBe(false)
	})
})
