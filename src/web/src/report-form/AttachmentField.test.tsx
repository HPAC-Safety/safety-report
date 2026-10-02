import { act, cleanup, fireEvent, render, screen } from "@testing-library/react"
import { useState } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"

vi.mock("../api/uploads", async (importOriginal) => ({
	...(await importOriginal<typeof import("../api/uploads")>()),
	uploadAttachment: vi.fn(),
	deleteUpload: vi.fn(async () => {}),
	exceedsKindLimit: vi.fn(() => false),
}))

import { UploadRejectedError, deleteUpload, exceedsKindLimit, uploadAttachment } from "../api/uploads"
import { AttachmentField, carriesFiles, type Attachment } from "./AttachmentField"

const upload = vi.mocked(uploadAttachment)
const del = vi.mocked(deleteUpload)
const tooLarge = vi.mocked(exceedsKindLimit)

const t = (key: string, params?: Record<string, string | number>) => (params?.name ? `${key}:${params.name}` : key)

const file = (name: string, size = 10) => new File([new Uint8Array(size)], name, { type: "image/png" })

function Harness({
	initial = [],
	remaining = 5,
	onBusy,
}: {
	initial?: Attachment[]
	remaining?: number
	onBusy?: (busy: boolean) => void
}) {
	const [rows, setRows] = useState<Attachment[]>(initial)
	return (
		<>
			<AttachmentField
				fieldId="q"
				describedBy="d"
				attachments={rows}
				onAttachmentsChange={setRows}
				onBusyChange={onBusy ?? (() => {})}
				remaining={remaining}
				t={t}
			/>
			<output data-testid="rows">{rows.map((row) => `${row.name}:${row.status}:${row.reason ?? ""}`).join("|")}</output>
		</>
	)
}

const choose = (...files: File[]) => fireEvent.change(document.getElementById("q") as HTMLInputElement, { target: { files } })
const rows = () => screen.getByTestId("rows").textContent
const flush = () => act(async () => {})

function deferred<T>() {
	let resolve!: (value: T) => void
	let reject!: (reason: unknown) => void
	const promise = new Promise<T>((res, rej) => {
		resolve = res
		reject = rej
	})
	return { promise, resolve, reject }
}

beforeEach(() => {
	upload.mockReset()
	del.mockClear()
	tooLarge.mockReset()
	tooLarge.mockReturnValue(false)
})

afterEach(cleanup)

describe("AttachmentField", () => {
	it("draws the drop zone with its guidance and no list while there are no rows", () => {
		render(<Harness />)

		expect(screen.getByTestId("attachment-drop-zone")).toBeTruthy()
		expect(document.getElementById("q-guidance")?.textContent).toBe("report.attachments.guidance")
		expect(screen.queryByRole("list")).toBeNull()
	})

	it("uploads a chosen file at once, shows it uploading, then keeps it and reports busy and idle", async () => {
		const pending = deferred<{ uploadId: string; kind: "image" }>()
		upload.mockReturnValue(pending.promise)
		const onBusy = vi.fn()
		render(<Harness onBusy={onBusy} />)

		choose(file("a.png", 2048))
		await flush()
		expect(screen.getByRole("progressbar").getAttribute("aria-label")).toBe("report.attachments.uploadingNamed:a.png")
		expect(onBusy).toHaveBeenLastCalledWith(true)

		pending.resolve({ uploadId: "u1", kind: "image" })
		await flush()

		expect(rows()).toBe("a.png:uploaded:")
		expect(screen.queryByRole("progressbar")).toBeNull()
		expect(screen.getByText("2 KB")).toBeTruthy()
		expect(onBusy).toHaveBeenLastCalledWith(false)
	})

	it("refuses a file over its kind's limit at once, without uploading it", async () => {
		tooLarge.mockReturnValue(true)
		render(<Harness />)

		choose(file("big.png", 3 * 1024 * 1024))
		await flush()

		expect(upload).not.toHaveBeenCalled()
		expect(rows()).toBe("big.png:rejected:too_large")
		expect(screen.getByRole("alert").textContent).toBe("report.attachments.rejected.too_large")
		expect(screen.getByText("3.0 MB")).toBeTruthy()
	})

	it("keeps the reason the API gave for a refused file", async () => {
		upload.mockRejectedValue(new UploadRejectedError("empty"))
		render(<Harness />)

		choose(file("a.png", 5))
		await flush()

		expect(rows()).toBe("a.png:rejected:empty")
		expect(screen.getByText("5 B")).toBeTruthy()
	})

	it("calls any other failure a network one", async () => {
		upload.mockRejectedValue(new Error("offline"))
		render(<Harness />)

		choose(file("a.png"))
		await flush()

		expect(rows()).toBe("a.png:rejected:network")
	})

	it("takes only as many files as the report has room for, refusing the rest as over the limit", async () => {
		upload.mockResolvedValue({ uploadId: "u", kind: "image" })
		render(<Harness remaining={1} />)

		choose(file("one.png"), file("two.png"))
		await flush()

		expect(upload).toHaveBeenCalledTimes(1)
		expect(rows()).toContain("two.png:rejected:limit")
		expect(rows()).toContain("one.png:uploaded:")
	})

	it("cancels an upload under way, and erases it if it finishes anyway", async () => {
		const pending = deferred<{ uploadId: string; kind: "image" }>()
		upload.mockReturnValue(pending.promise)
		render(<Harness />)

		choose(file("a.png"))
		await flush()
		fireEvent.click(screen.getByRole("button", { name: "report.attachments.cancelNamed:a.png" }))
		expect(screen.queryByRole("progressbar")).toBeNull()

		pending.resolve({ uploadId: "late", kind: "image" })
		await flush()

		expect(del).toHaveBeenCalledWith("late")
		expect(rows()).toBe("")
	})

	it("says nothing when a cancelled upload then fails", async () => {
		const pending = deferred<{ uploadId: string; kind: "image" }>()
		upload.mockReturnValue(pending.promise)
		render(<Harness />)

		choose(file("a.png"))
		await flush()
		fireEvent.click(screen.getByRole("button", { name: "report.attachments.cancelNamed:a.png" }))
		pending.reject(new Error("aborted"))
		await flush()

		expect(rows()).toBe("")
	})

	it("cancels only the upload it is asked to when several are under way", async () => {
		upload.mockReturnValue(new Promise(() => {}))
		render(<Harness />)

		choose(file("a.png"), file("b.png"))
		await flush()
		fireEvent.click(screen.getByRole("button", { name: "report.attachments.cancelNamed:b.png" }))

		expect(screen.getAllByRole("progressbar")).toHaveLength(1)
	})

	it("removes an uploaded row and erases its upload, but erases nothing for a refused one", () => {
		render(
			<Harness
				initial={[
					{ key: "k1", name: "kept.png", size: 1, status: "uploaded", uploadId: "u1" },
					{ key: "k2", name: "bad.png", size: 1, status: "rejected", reason: "empty" },
					{ key: "k3", name: "gone.png", size: 1, status: "expired", uploadId: "u3" },
					{ key: "k4", name: "odd.png", size: 1, status: "uploaded" },
				]}
			/>,
		)

		fireEvent.click(screen.getByRole("button", { name: "report.attachments.removeNamed:kept.png" }))
		expect(del).toHaveBeenCalledWith("u1")
		fireEvent.click(screen.getByRole("button", { name: "report.attachments.removeNamed:bad.png" }))
		fireEvent.click(screen.getByRole("button", { name: "report.attachments.removeNamed:gone.png" }))
		fireEvent.click(screen.getByRole("button", { name: "report.attachments.removeNamed:odd.png" }))

		expect(del).toHaveBeenCalledTimes(1)
		expect(rows()).toBe("")
	})

	it("explains an expired row and a refused row with no reason", () => {
		render(
			<Harness
				initial={[
					{ key: "k1", name: "old.png", size: 1, status: "expired" },
					{ key: "k2", name: "bad.png", size: 1, status: "rejected" },
				]}
			/>,
		)

		expect(screen.getAllByRole("alert").map((alert) => alert.textContent)).toEqual([
			"report.attachments.expired",
			"report.attachments.rejected.unknown",
		])
	})

	it("abandons uploads still under way when it leaves the page", async () => {
		let signal: AbortSignal | undefined
		upload.mockImplementation((_file, aborter) => {
			signal = aborter
			return new Promise(() => {})
		})
		const onBusy = vi.fn()
		const { unmount } = render(<Harness onBusy={onBusy} />)

		choose(file("a.png"))
		await flush()
		unmount()

		expect(signal?.aborted).toBe(true)
		expect(onBusy).toHaveBeenLastCalledWith(false)
	})

	it("re-exports the stray-drop rule", () => {
		expect(carriesFiles({ dataTransfer: { types: ["Files"] } as unknown as DataTransfer })).toBe(true)
		expect(carriesFiles({ dataTransfer: null })).toBe(false)
	})
})
