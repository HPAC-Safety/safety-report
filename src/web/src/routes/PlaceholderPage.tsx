import { PlaceholderPageView, type PlaceholderPageViewProps } from "./PlaceholderPage.view"

/** Shared shell for every dummy nav destination in this spike — no real content yet. */
export function PlaceholderPage(props: PlaceholderPageViewProps) {
	return <PlaceholderPageView {...props} />
}
