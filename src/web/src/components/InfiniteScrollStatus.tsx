import { InfiniteScrollStatusView, type InfiniteScrollStatusViewProps } from "./InfiniteScrollStatus.view"

/** A markup-only component: the pass-through the split convention asks for (ADR-0188). */
export function InfiniteScrollStatus(props: InfiniteScrollStatusViewProps) {
	return <InfiniteScrollStatusView {...props} />
}
