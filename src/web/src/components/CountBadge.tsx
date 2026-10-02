import { CountBadgeView, type CountBadgeViewProps } from "./CountBadge.view"

/** A markup-only component: the pass-through the split convention asks for (ADR-0188). */
export function CountBadge(props: CountBadgeViewProps) {
	return <CountBadgeView {...props} />
}
