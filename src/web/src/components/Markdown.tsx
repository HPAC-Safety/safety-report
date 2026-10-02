import { MarkdownView, type MarkdownViewProps } from "./Markdown.view"

/*
 * The one Markdown renderer (ADR-0180, REQ-WLD-045). A markup-only component:
 * the pass-through the split convention asks for (ADR-0188). The react-markdown
 * configuration is pure markup configuration and lives in the view.
 */
export function Markdown(props: MarkdownViewProps) {
	return <MarkdownView {...props} />
}
