import type { AttachmentLightboxMediaModel, AttachmentLightboxMediaProps } from "./AttachmentLightboxMedia"

export type AttachmentLightboxMediaViewProps = AttachmentLightboxMediaProps & AttachmentLightboxMediaModel

/** The open item: an image, or a video with native controls; an empty box while its link loads. */
export function AttachmentLightboxMediaView({ item, label, url, video, failed, loaded, track }: AttachmentLightboxMediaViewProps) {
	if (!url) {
		return <div className="flex h-64 w-full items-center justify-center" />
	}

	return item.kind === "image" ? (
		<img src={url} alt={label} onError={failed} className="max-h-[70vh] w-full rounded object-contain" />
	) : (
		// eslint-disable-next-line jsx-a11y/media-has-caption -- a reporter's footage carries no captions, a deliberate position (REQ-MED-062)
		<video
			ref={video}
			src={url}
			aria-label={label}
			controls
			autoPlay
			playsInline
			preload="metadata"
			onError={failed}
			onLoadedMetadata={loaded}
			onTimeUpdate={track}
			onPlay={track}
			onPause={track}
			className="max-h-[70vh] w-full rounded bg-surface-2"
		/>
	)
}
