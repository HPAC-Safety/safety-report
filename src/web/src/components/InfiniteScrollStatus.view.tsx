import { useLocale } from "../i18n/useLocale"

/*
 * The tail of an infinite-scrolled list (issue no. 572): an invisible
 * sentinel that triggers the next page as it nears the viewport, a fallback
 * "Load more" control kept reachable by Tab but visually hidden until it
 * holds keyboard focus (so a sighted mouse visitor never sees a control
 * auto-load already made unnecessary), a "Retry" control shown
 * unconditionally once a page fails to load, and a polite `aria-live` region
 * announcing what just loaded for a visitor who is not watching the page
 * (product decision on issue no. 572, 2026-09-27).
 */
export interface InfiniteScrollStatusViewProps {
	hasMore: boolean
	loadingMore: boolean
	failed: boolean
	onLoadMore: () => void
	sentinelRef: (node: Element | null) => void
	announcement: string
}

export function InfiniteScrollStatusView({
	hasMore,
	loadingMore,
	failed,
	onLoadMore,
	sentinelRef,
	announcement,
}: InfiniteScrollStatusViewProps) {
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
				<div className="relative mt-6 flex justify-center">
					<button
						type="button"
						onClick={onLoadMore}
						disabled={loadingMore}
						className={
							failed
								? "touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2 disabled:opacity-50"
								: // Every utility that would give the hidden state real size
									// (padding, border) is itself moved behind `focus-visible:`,
									// because `sr-only`'s own `padding:0`/`border-width:0` reset
									// is what a plain `border`/`px-4` class would otherwise beat
									// in the cascade, clamping the box well past 1px regardless
									// of `width:1px`.
									"sr-only min-w-0 inline-flex items-center rounded font-sans text-sm text-ink disabled:opacity-50 focus-visible:not-sr-only focus-visible:touch-target focus-visible:border focus-visible:border-rule focus-visible:bg-surface focus-visible:px-4 focus-visible:hover:bg-surface-2"
						}
					>
						{failed ? t("list.retry") : loadingMore ? t("list.loadingMore") : t("list.loadMore")}
					</button>
				</div>
			)}
		</>
	)
}
