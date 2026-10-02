import { useState } from "react"
import type { PrivateAttachment } from "../api/adminReports"
import { PrivateAttachmentItemView } from "./PrivateAttachmentItem.view"

// Split across lines on purpose: tools/web/check-hardcoded-strings.mjs is a line
// scanner, and `=> Promise<…>` on one line reads to it as JSX text.
type Attempt = () =>
	Promise<void>

export interface PrivateAttachmentItemProps {
	attachment: PrivateAttachment
	size: string
	addedAt: string
	onDownload: () => void
	onRemove: Attempt
}

/** The view model of one row: whether its Remove is awaiting confirmation, and closing it once removed. */
export function usePrivateAttachmentItem({ onRemove }: PrivateAttachmentItemProps) {
	const [confirming, setConfirming] = useState(false)

	return {
		confirming,
		onAskRemove: () => setConfirming(true),
		onKeep: () => setConfirming(false),
		onConfirmRemove: () => void onRemove().then(() => setConfirming(false)),
	}
}

export type PrivateAttachmentItemModel = ReturnType<typeof usePrivateAttachmentItem>

export function PrivateAttachmentItem(props: PrivateAttachmentItemProps) {
	return <PrivateAttachmentItemView {...props} {...usePrivateAttachmentItem(props)} />
}
