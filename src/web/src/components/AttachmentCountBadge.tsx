import { useLocale } from "../i18n/useLocale"

/*
 * A report's attachment icon and count, on the public feed and the admin
 * report list (issue #427 decisions 1-2). The count is already viewer-scoped
 * by the API — this component only decides whether to show it (omitted at
 * zero) and how to label it accessibly.
 */
export function AttachmentCountBadge({ count }: { count: number }) {
	const { t } = useLocale()

	if (count <= 0) {
		return null
	}

	const label = t(count === 1 ? "attachments.count.one" : "attachments.count.other", { count: String(count) })

	return (
		<span className="inline-flex items-center gap-1 font-sans text-sm text-ink-muted" data-attachment-count={count}>
			<PaperclipIcon />
			<span>{label}</span>
		</span>
	)
}

function PaperclipIcon() {
	return (
		<svg
			aria-hidden="true"
			viewBox="0 0 24 24"
			width="16"
			height="16"
			fill="none"
			stroke="currentColor"
			strokeWidth="2"
			strokeLinecap="round"
			strokeLinejoin="round"
		>
			<path d="M21.44 11.05 12.25 20.24a5 5 0 0 1-7.07-7.07l9.19-9.19a3.5 3.5 0 0 1 4.95 4.95L10.13 17.12a2 2 0 0 1-2.83-2.83l8.49-8.48" />
		</svg>
	)
}
