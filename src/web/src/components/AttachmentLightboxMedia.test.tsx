import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "../api/adminQuestions"
import { AttachmentLightboxMedia, type AttachmentLightboxMediaProps } from "./AttachmentLightboxMedia"
import type { StripItem } from "./stripItems"

afterEach(cleanup)

const image: StripItem = { id: "i", kind: "image", format: null, state: "ready", visibility: null }
const video: StripItem = { id: "v", kind: "video", format: null, state: "ready", visibility: null }

function mount(over: Partial<AttachmentLightboxMediaProps> = {}) {
	const props: AttachmentLightboxMediaProps = {
		item: image,
		label: "Photo 1 of 1",
		getLink: vi.fn().mockResolvedValue("https://files/1"),
		invalidateLink: vi.fn(),
		onGone: vi.fn(),
		...over,
	}
	const view = render(<AttachmentLightboxMedia {...props} />)
	return { props, ...view }
}

const videoElement = () => document.querySelector("video") as HTMLVideoElement

function stubVideo(element: HTMLVideoElement, state: { currentTime: number; paused: boolean; readyState: number }) {
	Object.defineProperty(element, "currentTime", { value: state.currentTime, writable: true, configurable: true })
	Object.defineProperty(element, "paused", { value: state.paused, configurable: true })
	Object.defineProperty(element, "readyState", { value: state.readyState, configurable: true })
	const pause = vi.fn()
	const play = vi.fn().mockResolvedValue(undefined)
	element.pause = pause
	element.play = play
	return { pause, play }
}

describe("AttachmentLightboxMedia", () => {
	it("shows an empty box until the link arrives, then the image", async () => {
		const { container } = mount()
		expect(container.querySelector("img")).toBeNull()
		expect((await screen.findByAltText("Photo 1 of 1")).getAttribute("src")).toBe("https://files/1")
	})

	it("tells the parent when the item is gone, and ignores other failures", async () => {
		const gone = mount({ getLink: vi.fn().mockRejectedValue(new ApiError(404, "gone")) })
		await waitFor(() => expect(gone.props.onGone).toHaveBeenCalled())
		cleanup()
		const other = mount({ getLink: vi.fn().mockRejectedValue(new Error("network")) })
		await act(async () => {})
		expect(other.props.onGone).not.toHaveBeenCalled()
	})

	it("uses the latest onGone callback", async () => {
		const first = vi.fn()
		const second = vi.fn()
		const getLink = vi.fn().mockRejectedValue(new ApiError(404, "gone"))
		const { rerender } = render(<AttachmentLightboxMedia item={image} label="l" getLink={getLink} invalidateLink={vi.fn()} onGone={first} />)
		await act(async () => {})
		rerender(<AttachmentLightboxMedia item={image} label="l" getLink={getLink} invalidateLink={vi.fn()} onGone={second} />)
		expect(first).toHaveBeenCalledTimes(1)
		expect(second).not.toHaveBeenCalled()
	})

	it("asks for a fresh link after an image fails, and gives up after two failures in a row", async () => {
		const { props } = mount()
		const img = await screen.findByAltText("Photo 1 of 1")
		fireEvent.error(img)
		expect(props.invalidateLink).toHaveBeenCalledWith("i")
		await waitFor(() => expect(props.getLink).toHaveBeenCalledTimes(2))
		fireEvent.error(screen.getByAltText("Photo 1 of 1"))
		expect(props.onGone).not.toHaveBeenCalled()
		fireEvent.error(screen.getByAltText("Photo 1 of 1"))
		expect(props.onGone).toHaveBeenCalledTimes(1)
	})

	it("resumes a playing video where it was after its link is replaced", async () => {
		mount({ item: video, label: "Video 1 of 1" })
		await screen.findByLabelText("Video 1 of 1")
		stubVideo(videoElement(), { currentTime: 12, paused: false, readyState: 4 })
		fireEvent.timeUpdate(videoElement())
		fireEvent.error(videoElement())
		await waitFor(() => expect(videoElement()).toBeTruthy())
		const { play } = stubVideo(videoElement(), { currentTime: 0, paused: true, readyState: 4 })
		fireEvent.loadedMetadata(videoElement())
		expect(videoElement().currentTime).toBe(12)
		expect(play).toHaveBeenCalled()
	})

	it("keeps a paused video paused after its link is replaced", async () => {
		const { props } = mount({ item: video, label: "Video 1 of 1" })
		await screen.findByLabelText("Video 1 of 1")
		stubVideo(videoElement(), { currentTime: 5, paused: true, readyState: 4 })
		fireEvent.pause(videoElement())
		fireEvent.error(videoElement())
		await waitFor(() => expect(props.getLink).toHaveBeenCalledTimes(2))
		const { pause, play } = stubVideo(videoElement(), { currentTime: 0, paused: false, readyState: 4 })
		fireEvent.loadedMetadata(videoElement())
		expect(videoElement().currentTime).toBe(5)
		expect(pause).toHaveBeenCalled()
		expect(play).not.toHaveBeenCalled()
	})

	it("loads normally when there is nothing to resume, and does not track a video that has not loaded", async () => {
		mount({ item: video, label: "Video 1 of 1" })
		await screen.findByLabelText("Video 1 of 1")
		const { pause } = stubVideo(videoElement(), { currentTime: 3, paused: false, readyState: 0 })
		fireEvent.play(videoElement())
		fireEvent.loadedMetadata(videoElement())
		expect(videoElement().currentTime).toBe(3)
		expect(pause).not.toHaveBeenCalled()
	})

	it("does not record the position while a resume is pending", async () => {
		const { props } = mount({ item: video, label: "Video 1 of 1" })
		await screen.findByLabelText("Video 1 of 1")
		stubVideo(videoElement(), { currentTime: 7, paused: false, readyState: 4 })
		fireEvent.timeUpdate(videoElement())
		fireEvent.error(videoElement())
		await waitFor(() => expect(props.getLink).toHaveBeenCalledTimes(2))
		stubVideo(videoElement(), { currentTime: 99, paused: false, readyState: 4 })
		fireEvent.timeUpdate(videoElement())
		fireEvent.loadedMetadata(videoElement())
		expect(videoElement().currentTime).toBe(7)
	})

	it("swallows a failed resume play", async () => {
		mount({ item: video, label: "Video 1 of 1" })
		await screen.findByLabelText("Video 1 of 1")
		stubVideo(videoElement(), { currentTime: 4, paused: false, readyState: 4 })
		fireEvent.timeUpdate(videoElement())
		fireEvent.error(videoElement())
		await act(async () => {})
		stubVideo(videoElement(), { currentTime: 0, paused: true, readyState: 4 })
		const play = vi.fn().mockRejectedValue(new Error("blocked"))
		videoElement().play = play
		fireEvent.loadedMetadata(videoElement())
		await act(async () => {})
		expect(play).toHaveBeenCalled()
	})
})
