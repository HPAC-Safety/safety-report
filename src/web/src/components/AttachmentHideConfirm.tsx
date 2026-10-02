import { useState } from "react"
import { AttachmentHideConfirmView } from "./AttachmentHideConfirm.view"

export interface AttachmentHideConfirmProps {
	onHide: () => void
}

/**
 * The view model of a two-step confirm before hiding, on both pages
 * (REQ-MED-035, REQ-MED-036): "Hide from the public" opens "Hide this from
 * everyone?" with confirm/keep.
 */
export function useAttachmentHideConfirm({ onHide }: AttachmentHideConfirmProps) {
	const [confirming, setConfirming] = useState(false)

	return {
		confirming,
		onAsk: () => setConfirming(true),
		onConfirm() {
			setConfirming(false)
			onHide()
		},
		onKeep: () => setConfirming(false),
	}
}

export type AttachmentHideConfirmModel = ReturnType<typeof useAttachmentHideConfirm>

export function AttachmentHideConfirm(props: AttachmentHideConfirmProps) {
	return <AttachmentHideConfirmView {...props} {...useAttachmentHideConfirm(props)} />
}
