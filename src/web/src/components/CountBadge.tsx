import { useLocale } from "../i18n/useLocale"

/*
 * How much work is waiting behind an Admin menu entry. A filled brand-red pill,
 * the one place red means "needs you" rather than a primary action or an error
 * (docs/design-system.md). Brand-700, not 600: white 12px text needs its 4.5:1.
 * Nothing renders for zero.
 */
export function CountBadge({ count }: { count: number | null | undefined }) {
	const { t } = useLocale()

	if (!count) return null

	return (
		<span data-count-badge={count} className="ml-2 inline-flex min-w-5 items-center justify-center rounded-full bg-brand-700 px-1.5 font-sans text-xs font-semibold leading-5 text-ink-inverse">
			<span aria-hidden="true">{count > 99 ? "99+" : count}</span>
			<span className="sr-only">{" "}{t("nav.pendingCount", { count: String(count) })}</span>
		</span>
	)
}
