import { useEffect, useState } from "react"
import { useLocation } from "react-router-dom"
import { getPendingCounts, type PendingCounts } from "../api/adminReports"

/**
 * The Admin menu's counts, fetched while `enabled` and again on every
 * navigation, so approving a report and going back shows the new number
 * (REQ-MOD-087). A failed read shows no count rather than a wrong one.
 */
export function usePendingCounts(enabled: boolean): PendingCounts | null {
	const { pathname } = useLocation()
	const [counts, setCounts] = useState<PendingCounts | null>(null)

	useEffect(() => {
		if (!enabled) {
			setCounts(null)
			return
		}

		let current = true
		getPendingCounts()
			.then((next) => {
				if (current) setCounts(next)
			})
			.catch(() => {
				if (current) setCounts(null)
			})

		return () => {
			current = false
		}
	}, [enabled, pathname])

	return counts
}
