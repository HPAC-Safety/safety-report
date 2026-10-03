import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { addPrivateAttachment, PRIVATE_ATTACHMENT_MAX_BYTES, PrivateUploadError, stagePrivateUpload } from "../api/adminReports"
import { LocaleContext } from "../i18n/LocaleProvider"
import { PrivateAttachmentStaging } from "./PrivateAttachmentStaging"

vi.mock("../api/adminReports", async (importOriginal) => ({
	...(await importOriginal<typeof import("../api/adminReports")>()),
	stagePrivateUpload: vi.fn(),
	addPrivateAttachment: vi.fn(),
}))

beforeEach(() => {
	vi.mocked(stagePrivateUpload).mockReset().mockResolvedValue({ uploadId: "u1", contentType: "text/plain" })
	vi.mocked(addPrivateAttachment).mockReset().mockResolvedValue(undefined as never)
})
afterEach(cleanup)

function wrapper({ children }: { children: ReactNode }) {
	return <LocaleContext.Provider value={{ locale: "en-CA", setLocale: () => {}, t: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key) }}>{children}</LocaleContext.Provider>
}

function mount(onAdded = vi.fn()) {
	const view = render(<PrivateAttachmentStaging reportId="r1" onAdded={onAdded} />, { wrapper })
	return { onAdded, ...view }
}

const input = () => document.getElementById("private-attachment-input") as HTMLInputElement
const choose = (...files: File[]) => fireEvent.change(input(), { target: { files } })
const file = (name = "a.txt") => new File(["x"], name)
const bigFile = () => Object.defineProperty(file("big.bin"), "size", { value: PRIVATE_ATTACHMENT_MAX_BYTES + 1 })

describe("PrivateAttachmentStaging", () => {
	it("shows only the drop zone, with the limit, until a file is staged", () => {
		mount()
		expect(screen.getByText('privateAttachments.limit {"max":"1 GB"}')).toBeTruthy()
		expect(screen.queryByRole("list")).toBeNull()
		expect(screen.queryByRole("button", { name: /addAll/ })).toBeNull()
	})

	it("refuses a file that is too large at once, without uploading", () => {
		mount()
		choose(bigFile())
		expect(stagePrivateUpload).not.toHaveBeenCalled()
		expect(screen.getByRole("alert").textContent).toBe('privateAttachments.error.too_large {"max":"1 GB"}')
	})

	it("uploads with progress, then asks for a description and offers Add", async () => {
		let report: (fraction: number) => void = () => {}
		let finish: (value: { uploadId: string; contentType: string }) => void = () => {}
		vi.mocked(stagePrivateUpload).mockImplementation((_report, _file, onProgress) => {
			report = onProgress
			return new Promise((resolve) => (finish = resolve))
		})
		mount()
		choose(file())
		expect(document.querySelector("progress")?.getAttribute("aria-label")).toBe('privateAttachments.uploadingNamed {"name":"a.txt"}')
		act(() => report(0.5))
		expect(screen.getByText('privateAttachments.progress {"percent":"50"}')).toBeTruthy()
		expect(screen.getByRole<HTMLButtonElement>("button", { name: /addAll/ }).disabled).toBe(true)
		await act(async () => finish({ uploadId: "u1", contentType: "text/plain" }))
		expect(screen.getByText('privateAttachments.descriptionLength {"count":"0","max":"500"}')).toBeTruthy()
		expect(screen.getByRole<HTMLButtonElement>("button", { name: "privateAttachments.addAll.one" }).disabled).toBe(false)
	})

	it("cancels an upload in progress and drops its row", async () => {
		vi.mocked(stagePrivateUpload).mockImplementation((_report, _file, _progress, signal: AbortSignal | undefined) => new Promise((_, reject) => signal?.addEventListener("abort", () => reject(new Error("aborted")))))
		mount()
		choose(file("slow.txt"))
		fireEvent.click(screen.getByRole("button", { name: 'privateAttachments.cancelNamed {"name":"slow.txt"}' }))
		await waitFor(() => expect(screen.queryByRole("list")).toBeNull())
	})

	it("abandons anything still uploading when the page is left", async () => {
		let signal: AbortSignal | undefined
		vi.mocked(stagePrivateUpload).mockImplementation((_report, _file, _progress, s) => {
			signal = s
			return new Promise(() => {})
		})
		const { unmount } = mount()
		choose(file())
		unmount()
		expect(signal?.aborted).toBe(true)
	})

	it("marks a failed upload by its reason, or as a network failure", async () => {
		vi.mocked(stagePrivateUpload).mockRejectedValueOnce(new PrivateUploadError("empty")).mockRejectedValueOnce(new PrivateUploadError("storage")).mockRejectedValueOnce(new Error("offline"))
		mount()
		choose(file("a"), file("b"), file("c"))
		await waitFor(() => expect(screen.getAllByRole("alert")).toHaveLength(3))
		expect(screen.getAllByRole("alert").map((el) => el.textContent)).toEqual([
			'privateAttachments.error.empty {"max":"1 GB"}',
			"privateAttachments.error.upload",
			"privateAttachments.error.upload",
		])
	})

	it("removes a settled row without calling the API", async () => {
		mount()
		choose(file("keep.txt"))
		await screen.findByLabelText("privateAttachments.descriptionLabel")
		fireEvent.click(screen.getByRole("button", { name: 'privateAttachments.removeStagedNamed {"name":"keep.txt"}' }))
		expect(screen.queryByRole("list")).toBeNull()
		expect(addPrivateAttachment).not.toHaveBeenCalled()
	})

	it("adds every finished row with its description and tells the caller", async () => {
		const { onAdded } = mount()
		choose(file("a.txt"), file("b.txt"))
		const boxes = await screen.findAllByLabelText("privateAttachments.descriptionLabel")
		fireEvent.change(boxes[0], { target: { value: "First" } })
		fireEvent.click(screen.getByRole("button", { name: 'privateAttachments.addAll.other {"count":2}' }))
		await waitFor(() => expect(onAdded).toHaveBeenCalled())
		expect(addPrivateAttachment).toHaveBeenCalledWith("r1", "u1", "a.txt", "First")
		expect(addPrivateAttachment).toHaveBeenCalledWith("r1", "u1", "b.txt", "")
		expect(screen.queryByRole("list")).toBeNull()
		expect(screen.queryByRole("alert")).toBeNull()
	})

	it("marks a row whose claim fails, says so, and still tells the caller", async () => {
		vi.mocked(addPrivateAttachment).mockRejectedValue(new Error("no"))
		const { onAdded } = mount()
		choose(file())
		await screen.findByLabelText("privateAttachments.descriptionLabel")
		fireEvent.click(screen.getByRole("button", { name: "privateAttachments.addAll.one" }))
		await waitFor(() => expect(onAdded).toHaveBeenCalled())
		expect(screen.getAllByRole("alert").map((el) => el.textContent)).toEqual(["privateAttachments.error.upload", "privateAttachments.error.upload"])
	})

	it("disables Add while a description is too long", async () => {
		mount()
		choose(file())
		const box = await screen.findByLabelText("privateAttachments.descriptionLabel")
		fireEvent.change(box, { target: { value: "x".repeat(501) } })
		expect(screen.getByRole<HTMLButtonElement>("button", { name: "privateAttachments.addAll.one" }).disabled).toBe(true)
		expect(screen.getByText('privateAttachments.descriptionLength {"count":"501","max":"500"}').className).toContain("text-brand-700")
	})

	it("skips a finished row that has no upload id", async () => {
		vi.mocked(stagePrivateUpload).mockResolvedValue({ uploadId: "", contentType: "" })
		const { onAdded } = mount()
		choose(file())
		await screen.findByLabelText("privateAttachments.descriptionLabel")
		fireEvent.click(screen.getByRole("button", { name: "privateAttachments.addAll.one" }))
		await waitFor(() => expect(onAdded).toHaveBeenCalled())
		expect(addPrivateAttachment).not.toHaveBeenCalled()
	})
})
