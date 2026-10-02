import { AttachmentCountBadgeView, type AttachmentCountBadgeViewProps } from "./AttachmentCountBadge.view"

/** A markup-only component: the pass-through the split convention asks for (ADR-0188). */
export function AttachmentCountBadge(props: AttachmentCountBadgeViewProps) {
	return <AttachmentCountBadgeView {...props} />
}
