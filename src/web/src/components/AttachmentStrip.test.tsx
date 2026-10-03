import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "../api/adminQuestions"
import { attachmentLink, attachmentOriginalLink, setAttachmentHidden, type ReportAttachment } from "../api/adminReports"
import { fetchMediaLink, PublicReportNotFound, type PublicMedia } from "../api/publicReports"
import { LocaleContext } from "../i18n/LocaleProvider"
import { AttachmentStrip, type AttachmentStripProps } from "./AttachmentStrip"
import type { StripItem } from "./stripItems"
import { present } from "../lib/present"

vi.mock("../api/adminReports", () => ({
	attachmentLink: vi.fn(),
	attachmentOriginalLink: vi.fn(),
	setAttachmentHidden: vi.fn(),
}))
vi.mock("../api/publicReports", async (importOriginal) => ({
	...(await importOriginal<typeof import("../api/publicReports")>()),
	fetchMediaLink: vi.fn(),
}))

const lightbox = vi.hoisted(() => ({ latest: null as null | { items: StripItem[]; getLink: (item: StripItem) => Promise<string>; invalidateLink: (id: string) => void; onClose: () => void; onGone: (id: string) => void } }))
vi.mock("./AttachmentLightbox", () => ({
	AttachmentLightbox: (props: NonNullable<typeof lightbox.latest> & { openId: string }) => {
		lightbox.latest = props
		return <div data-testid="lightbox" data-open={props.openId} />
	},
}))

const assign = vi.fn()
/** The lightbox the strip last rendered; a test that calls it has opened one. */
const openLightbox = () => present(lightbox.latest, "the open lightbox")
const future = () => new Date(Date.now() + 60_000).toISOString()

beforeEach(() => {
	assign.mockReset()
	lightbox.latest = null
	Object.defineProperty(window, "location", { value: { assign }, configurable: true })
	vi.mocked(fetchMediaLink).mockReset().mockImplementation((_report, id) => Promise.resolve({ url: `https://pub/${id}`, expiresAt: future() }))
	vi.mocked(attachmentLink).mockReset().mockImplementation((_report, a) => Promise.resolve({ url: `https://staff/${a.id}`, expiresAt: future(), fileName: "f" }))
	vi.mocked(attachmentOriginalLink).mockReset().mockResolvedValue({ url: "https://orig", expiresAt: future(), fileName: "f" })
	vi.mocked(setAttachmentHidden).mockReset().mockResolvedValue(undefined)
})
afterEach(cleanup)

function wrapper({ children }: { children: ReactNode }) {
	return <LocaleContext.Provider value={{ locale: "en-CA", setLocale: () => {}, t: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key) }}>{children}</LocaleContext.Provider>
}

const publicMedia: PublicMedia[] = [
	{ id: "img", kind: "image", format: null },
	{ id: "vid", kind: "video", format: null },
	{ id: "doc", kind: "document", format: "pdf" },
]

const staffAttachments: ReportAttachment[] = [
	{ id: "pub", kind: "image", state: "ready", visibility: "public", format: null },
	{ id: "hid", kind: "image", state: "ready", visibility: "hidden", format: null },
	{ id: "proc", kind: "image", state: "processing", visibility: "private", format: null },
	{ id: "fail", kind: "video", state: "failed", visibility: "private", format: null },
	{ id: "sdoc", kind: "document", state: "ready", visibility: "private", format: null },
]

function mount(over: Partial<AttachmentStripProps> = {}) {
	const props: AttachmentStripProps = { reportId: "r1", media: publicMedia, staffAttachments: null, onChanged: vi.fn(), ...over }
	const view = render(<AttachmentStrip {...props} />, { wrapper })
	return { props, ...view }
}

const button = (name: string) => screen.getByRole("button", { name })

describe("AttachmentStrip for the public", () => {
	it("renders nothing when there are no items", () => {
		const { container } = mount({ media: [] })
		expect(container.innerHTML).toBe("")
	})

	it("lists each item with a generic, numbered label", async () => {
		mount()
		expect(screen.getAllByRole("listitem").map((li) => li.getAttribute("data-media"))).toEqual(["image", "video", "document"])
		expect(button('media.photoLabel {"index":1,"count":1}')).toBeTruthy()
		expect(button('media.videoLabel {"index":1,"count":1}')).toBeTruthy()
		expect(button('media.downloadLabel {"label":"media.documentLabel {\\"index\\":1,\\"count\\":1,\\"format\\":\\"PDF\\"}"}')).toBeTruthy()
		await waitFor(() => expect(document.querySelector("img")?.getAttribute("src")).toBe("https://pub/img"))
	})

	it("labels a document with no format by an empty one", () => {
		mount({ media: [{ id: "d", kind: "document", format: null }] })
		expect(button('media.downloadLabel {"label":"media.documentLabel {\\"index\\":1,\\"count\\":1,\\"format\\":\\"\\"}"}')).toBeTruthy()
	})

	it("downloads a document through the public link", async () => {
		mount()
		fireEvent.click(screen.getByRole("button", { name: /media.downloadLabel/ }))
		await waitFor(() => expect(assign).toHaveBeenCalledWith("https://pub/doc"))
	})

	it("shows a download error, and drops a document that has gone", async () => {
		vi.mocked(fetchMediaLink).mockRejectedValueOnce(new Error("boom"))
		mount({ media: [publicMedia[2]] })
		fireEvent.click(screen.getByRole("button", { name: /media.downloadLabel/ }))
		expect((await screen.findByRole("alert")).textContent).toBe("media.error.download")
		vi.mocked(fetchMediaLink).mockRejectedValueOnce(new PublicReportNotFound())
		fireEvent.click(screen.getByRole("button", { name: /media.downloadLabel/ }))
		await waitFor(() => expect(screen.queryByTestId("attachment-strip")).toBeNull())
	})

	it("opens the lightbox on an image and returns focus to its thumbnail on close", () => {
		mount()
		const trigger = button('media.photoLabel {"index":1,"count":1}')
		fireEvent.click(trigger)
		expect(screen.getByTestId("lightbox").getAttribute("data-open")).toBe("img")
		expect(lightbox.latest?.items.map((item) => item.id)).toEqual(["img", "vid"])
		act(() => openLightbox().onClose())
		expect(screen.queryByTestId("lightbox")).toBeNull()
		expect(document.activeElement).toBe(trigger)
	})

	it("shares one link between a thumbnail and the lightbox until it is nearly expired or invalidated", async () => {
		mount()
		await waitFor(() => expect(fetchMediaLink).toHaveBeenCalledTimes(1))
		fireEvent.click(button('media.photoLabel {"index":1,"count":1}'))
		await expect(openLightbox().getLink(openLightbox().items[0])).resolves.toBe("https://pub/img")
		expect(fetchMediaLink).toHaveBeenCalledTimes(1)
		openLightbox().invalidateLink("img")
		await openLightbox().getLink(openLightbox().items[0])
		expect(fetchMediaLink).toHaveBeenCalledTimes(2)
		vi.mocked(fetchMediaLink).mockResolvedValueOnce({ url: "https://pub/short", expiresAt: new Date(Date.now() + 1000).toISOString() })
		openLightbox().invalidateLink("img")
		await openLightbox().getLink(openLightbox().items[0])
		await openLightbox().getLink(openLightbox().items[0])
		expect(fetchMediaLink).toHaveBeenCalledTimes(4)
	})

	it("removes an item the lightbox reports gone", () => {
		mount()
		fireEvent.click(button('media.photoLabel {"index":1,"count":1}'))
		act(() => openLightbox().onGone("vid"))
		expect(screen.getAllByRole("listitem")).toHaveLength(2)
	})

	it("drops a thumbnail whose image is gone", async () => {
		vi.mocked(fetchMediaLink).mockRejectedValueOnce(new PublicReportNotFound())
		mount({ media: [publicMedia[0], publicMedia[1]] })
		await waitFor(() => expect(screen.getAllByRole("listitem")).toHaveLength(1))
	})

	it("follows a changed public list", () => {
		const { rerender, props } = mount()
		rerender(<AttachmentStrip {...props} media={[publicMedia[0]]} />)
		expect(screen.getAllByRole("listitem")).toHaveLength(1)
	})
})

describe("AttachmentStrip for staff", () => {
	it("marks each file by its state or visibility and offers Hide or Show", () => {
		mount({ staffAttachments })
		expect(screen.getAllByTestId("attachment-visibility").map((el) => el.getAttribute("data-visibility"))).toEqual(["public", "hidden", "private"])
		expect(screen.getAllByTestId("attachment-state").map((el) => el.textContent)).toEqual([
			"reports.attachment.state.processing",
			"reports.attachment.state.failed",
		])
		expect(screen.getAllByRole("button", { name: "reports.attachment.hide" })).toHaveLength(1)
		expect(screen.getAllByRole("button", { name: "reports.attachment.show" })).toHaveLength(1)
	})

	it("offers Hide for a file public once published too", () => {
		mount({ staffAttachments: [{ ...staffAttachments[0], visibility: "when_published" }] })
		expect(screen.getAllByRole("button", { name: "reports.attachment.hide" })).toHaveLength(1)
	})

	it("hides after a confirmation and tells the caller", async () => {
		const { props } = mount({ staffAttachments })
		fireEvent.click(button("reports.attachment.hide"))
		fireEvent.click(button("media.hide"))
		await waitFor(() => expect(props.onChanged).toHaveBeenCalled())
		expect(setAttachmentHidden).toHaveBeenCalledWith("r1", "pub", true)
	})

	it("shows a hidden file again", async () => {
		const { props } = mount({ staffAttachments })
		fireEvent.click(button("reports.attachment.show"))
		await waitFor(() => expect(props.onChanged).toHaveBeenCalled())
		expect(setAttachmentHidden).toHaveBeenCalledWith("r1", "hid", false)
	})

	it("reports a failed hide or show", async () => {
		vi.mocked(setAttachmentHidden).mockRejectedValue(new Error("no"))
		const { props } = mount({ staffAttachments })
		fireEvent.click(button("reports.attachment.show"))
		expect((await screen.findByRole("alert")).textContent).toBe("media.error.hide")
		cleanup()
		mount({ staffAttachments })
		fireEvent.click(button("reports.attachment.hide"))
		fireEvent.click(button("media.hide"))
		expect((await screen.findByRole("alert")).textContent).toBe("media.error.hide")
		expect(props.onChanged).not.toHaveBeenCalled()
	})

	it("downloads a document through the audited staff link", async () => {
		mount({ staffAttachments })
		fireEvent.click(screen.getByRole("button", { name: /media.downloadLabel/ }))
		await waitFor(() => expect(assign).toHaveBeenCalledWith("https://staff/sdoc"))
		expect(attachmentLink).toHaveBeenCalledWith("r1", expect.objectContaining({ kind: "document", visibility: "private" }))
	})

	it("downloads a document with no recorded visibility as private", async () => {
		mount({ staffAttachments: [{ ...staffAttachments[4], visibility: null as unknown as ReportAttachment["visibility"] }] })
		fireEvent.click(screen.getByRole("button", { name: /media.downloadLabel/ }))
		await waitFor(() => expect(attachmentLink).toHaveBeenCalledWith("r1", expect.objectContaining({ visibility: "private" })))
	})

	it("downloads the original of a file still processing or failed, never opening the lightbox", async () => {
		mount({ staffAttachments })
		fireEvent.click(button('media.photoLabel {"index":3,"count":3}'))
		await waitFor(() => expect(assign).toHaveBeenCalledWith("https://orig"))
		expect(screen.queryByTestId("lightbox")).toBeNull()
		expect(attachmentOriginalLink).toHaveBeenCalledWith("r1", "proc")
	})

	it("shows an error when the original cannot be fetched, and drops it when gone", async () => {
		vi.mocked(attachmentOriginalLink).mockRejectedValueOnce(new Error("no"))
		mount({ staffAttachments })
		fireEvent.click(button('media.videoLabel {"index":1,"count":1}'))
		expect((await screen.findByRole("alert")).textContent).toBe("media.error.download")
		vi.mocked(attachmentOriginalLink).mockRejectedValueOnce(new ApiError(404, "gone"))
		fireEvent.click(button('media.videoLabel {"index":1,"count":1}'))
		await waitFor(() => expect(screen.getAllByRole("listitem")).toHaveLength(4))
	})

	it("shows a staff document error and drops a document that has gone", async () => {
		vi.mocked(attachmentLink).mockRejectedValueOnce(new Error("no"))
		mount({ staffAttachments: [staffAttachments[4]] })
		fireEvent.click(screen.getByRole("button", { name: /media.downloadLabel/ }))
		expect((await screen.findByRole("alert")).textContent).toBe("media.error.download")
		vi.mocked(attachmentLink).mockRejectedValueOnce(new ApiError(404, "gone"))
		fireEvent.click(screen.getByRole("button", { name: /media.downloadLabel/ }))
		await waitFor(() => expect(screen.queryByTestId("attachment-strip")).toBeNull())
	})

	it("mints a staff link for a non-public image thumbnail", async () => {
		mount({ staffAttachments: [staffAttachments[1]] })
		await waitFor(() => expect(attachmentLink).toHaveBeenCalledWith("r1", expect.objectContaining({ id: "hid" })))
	})
})
