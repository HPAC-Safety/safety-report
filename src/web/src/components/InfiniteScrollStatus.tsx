import { useLocale } from "../i18n/useLocale"

/*
 * The tail of an infinite-scrolled list (issue no. 572): an invisible
 * sentinel that triggers the next page as it nears the viewport, an
 * always-present button offering the same action for a keyboard or
 * screen-reader visitor and reading "Retry" once a page fails to load, and a
 * polite `aria-live` region announcing what just loaded for a visitor who is
 * not watching the page.
 */
export function InfiniteScrollStatus({
	hasMore,
	loadingMore,
	failed,
	onLoadMore,
	sentinelRef,
	announcement,
}: {
	hasMore: boolean
	loadingMore: boolean
	failed: boolean
	onLoadMore: () => void
	sentinelRef: (node: Element | null) => void
	announcement: string
}) {
	const { t } = useLocale()

	return (
		<>
			{hasMore && !failed && <div ref={sentinelRef} aria-hidden="true" data-infinite-scroll-sentinel />}

			<div aria-live="polite" className="sr-only">
				{announcement}
			</div>

			{failed && (
				<p role="alert" className="mt-6 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{t("list.error")}
				</p>
			)}

			{(hasMore || failed) && (
				<div className="mt-6 flex justify-center">
					<button
						type="button"
						onClick={onLoadMore}
						disabled={loadingMore}
						className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2 disabled:opacity-50"
					>
						{failed ? t("list.retry") : loadingMore ? t("list.loadingMore") : t("list.loadMore")}
					</button>
				</div>
			)}
		</>
	)
}
