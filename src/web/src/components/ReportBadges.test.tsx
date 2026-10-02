import { render, cleanup } from "@testing-library/react"
import { afterEach, describe, expect, it } from "vitest"
import { ReportBadges } from "./ReportBadges"
import type { ReactNode } from "react"
import { LocaleContext, type LocaleContextValue } from "../i18n/LocaleProvider"

afterEach(cleanup)

const value: LocaleContextValue = {
	locale: "en-CA",
	setLocale: () => {},
	t: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key),
}

function wrapper({ children }: { children: ReactNode }) {
	return <LocaleContext.Provider value={value}>{children}</LocaleContext.Provider>
}

const badges = (container: HTMLElement) =>
	[...container.querySelectorAll("[data-badge]")].map((badge) => badge.getAttribute("data-badge"))

describe("ReportBadges", () => {
	it("always shows the status badge", () => {
		const { container } = render(<ReportBadges status="pending" consent={true} isStuck={false} />, { wrapper })
		expect(badges(container)).toEqual(["status"])
		expect(container.textContent).toBe("reports.status.pending")
	})

	it("adds the private badge only when consent was refused", () => {
		const refused = render(<ReportBadges status="pending" consent={false} isStuck={false} />, { wrapper })
		expect(badges(refused.container)).toEqual(["status", "private"])
		const unanswered = render(<ReportBadges status="pending" consent={null} isStuck={false} />, { wrapper })
		expect(badges(unanswered.container)).toEqual(["status"])
	})

	it("adds the stuck badge when the report is stuck", () => {
		const { container } = render(<ReportBadges status="submitted" consent={true} isStuck={true} />, { wrapper })
		expect(badges(container)).toEqual(["status", "stuck"])
	})
})
