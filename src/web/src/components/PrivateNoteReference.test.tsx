import { act, render, renderHook, screen } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"

const download = vi.hoisted(() => vi.fn())
vi.mock("./PrivateAttachments", () => ({ downloadPrivateAttachment: download }))
vi.mock("./PrivateNoteReference.view", () => ({
	PrivateNoteReferenceView: (props: { reportId: string; failed: boolean }) => (
		<p data-testid="view">{props.reportId}:{String(props.failed)}</p>
	),
}))

import { PrivateNoteReference, usePrivateNoteReference } from "./PrivateNoteReference"

const props = { reportId: "r1", attachment: { id: "a1", fileName: "one.pdf", removed: false } }

describe("usePrivateNoteReference", () => {
	beforeEach(() => download.mockReset())

	it("starts without a failure", () => {
		const { result } = renderHook(() => usePrivateNoteReference(props))

		expect(result.current.failed).toBe(false)
	})

	it("downloads the named attachment of the report", async () => {
		download.mockResolvedValue(undefined)
		const { result } = renderHook(() => usePrivateNoteReference(props))

		await act(async () => {
			result.current.download("a1")
			await Promise.resolve()
		})

		expect(download).toHaveBeenCalledWith("r1", "a1")
		expect(result.current.failed).toBe(false)
	})

	it("reports a failed download and clears it on the next try", async () => {
		download.mockRejectedValueOnce(new Error("no")).mockResolvedValueOnce(undefined)
		const { result } = renderHook(() => usePrivateNoteReference(props))

		await act(async () => {
			result.current.download("a1")
			await Promise.resolve()
		})
		expect(result.current.failed).toBe(true)

		await act(async () => {
			result.current.download("a1")
			await Promise.resolve()
		})
		expect(result.current.failed).toBe(false)
	})
})

describe("PrivateNoteReference", () => {
	it("renders its view with the view model", () => {
		render(<PrivateNoteReference {...props} />)

		expect(screen.getByTestId("view").textContent).toBe("r1:false")
	})
})
