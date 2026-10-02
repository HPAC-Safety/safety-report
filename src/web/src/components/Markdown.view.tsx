import type { ComponentPropsWithoutRef, ReactNode } from "react"
import ReactMarkdown, { type Components } from "react-markdown"
import remarkBreaks from "remark-breaks"

/*
 * The one place Markdown becomes markup (ADR-0180, REQ-WLD-045). A summary, and a
 * reporter's paragraph answer with its Worker translation, are written in it;
 * nothing else in the app is.
 *
 * Only a safe subset is ever rendered: headings, paragraphs, bold, italic, lists
 * and line breaks. Raw HTML is never parsed, so it shows as the text it was written
 * as. A link is its text only, so the reader's browser is never sent anywhere, and
 * an image is dropped, so it can never make the browser fetch anything. Anything
 * else (a table, a quote, code) shows its text without its formatting. A single
 * newline is a line break, so a reporter's own line breaks survive.
 */

const ALLOWED_ELEMENTS = ["h1", "h2", "h3", "h4", "h5", "h6", "p", "strong", "em", "ul", "ol", "li", "br"]

const HEADING_CLASSES = [
	"mt-6 font-display text-2xl font-bold text-ink first:mt-0",
	"mt-6 font-display text-2xl font-bold text-ink first:mt-0",
	"mt-5 font-display text-lg font-semibold text-ink first:mt-0",
	"mt-4 font-sans text-base font-semibold text-ink first:mt-0",
	"mt-4 font-sans text-base font-semibold text-ink first:mt-0",
	"mt-4 font-sans text-base font-semibold text-ink first:mt-0",
]

const HEADING_TAGS = ["h1", "h2", "h3", "h4", "h5", "h6"] as const

const cache = new Map<number, Components>()

/**
 * The renderers for one place a Markdown text is shown. A `##` in the text is the
 * page's `h2` when `headingOffset` is 0; a page that already has its own headings
 * passes how many levels deeper the text's headings belong, so the outline never
 * skips a level or repeats one. One set per offset, so a component's identity is
 * stable between renders.
 */
function componentsFor(headingOffset: number): Components {
	const cached = cache.get(headingOffset)
	if (cached) return cached

	const heading = (level: number) => {
		const shown = Math.min(6, level + headingOffset)
		const Tag = HEADING_TAGS[shown - 1]
		return function Heading({ children }: { children?: ReactNode }) {
			return <Tag className={HEADING_CLASSES[shown - 1]}>{children}</Tag>
		}
	}

	const components: Components = {
		h1: heading(1),
		h2: heading(2),
		h3: heading(3),
		h4: heading(4),
		h5: heading(5),
		h6: heading(6),
		p: ({ children }: ComponentPropsWithoutRef<"p">) => <p className="mt-3 first:mt-0">{children}</p>,
		ul: ({ children }: ComponentPropsWithoutRef<"ul">) => <ul className="mt-3 list-disc pl-6 first:mt-0">{children}</ul>,
		ol: ({ children }: ComponentPropsWithoutRef<"ol">) => <ol className="mt-3 list-decimal pl-6 first:mt-0">{children}</ol>,
	}

	cache.set(headingOffset, components)
	return components
}

/** A test hook such as `data-summary` goes straight onto the wrapper. */
type DataAttributes = { [key: `data-${string}`]: string | undefined }

export type MarkdownViewProps = DataAttributes & {
	/** The text, as written. */
	children: string
	/** How many levels deeper than the text's own a heading is shown (0 shows `##` as an `h2`). */
	headingOffset?: number
	/** The language the text is written in, when it is not the page's. */
	lang?: string
	className?: string
}

export function MarkdownView({ children, headingOffset = 0, lang, className, ...data }: MarkdownViewProps) {
	return (
		<div lang={lang} className={className} {...data}>
			<ReactMarkdown
				allowedElements={ALLOWED_ELEMENTS}
				unwrapDisallowed
				remarkPlugins={[remarkBreaks]}
				components={componentsFor(headingOffset)}
			>
				{children}
			</ReactMarkdown>
		</div>
	)
}
