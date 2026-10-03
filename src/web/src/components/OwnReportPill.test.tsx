import { cleanup, render, screen } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, describe, expect, it } from "vitest"
import { LocaleContext, type LocaleContextValue } from "../i18n/LocaleProvider"
import { OwnReportPill } from "./OwnReportPill"

afterEach(cleanup)

const value: LocaleContextValue = { locale: "en-CA", setLocale: () => {}, t: (key) => key }

function wrapper({ children }: { children: ReactNode }) {
	return <LocaleContext.Provider value={value}>{children}</LocaleContext.Provider>
}

describe("OwnReportPill", () => {
	it("says a report for publication is not yet published", () => {
		const { container } = render(<OwnReportPill forPublication />, { wrapper })

		expect(screen.getByText("feed.own.notYetPublished")).toBeTruthy()
		expect(container.querySelector("[data-own-pill='not-yet-published']")).not.toBeNull()
	})

	it("says a report without publication consent is not for publication", () => {
		const { container } = render(<OwnReportPill forPublication={false} />, { wrapper })

		expect(screen.getByText("feed.own.notForPublication")).toBeTruthy()
		expect(container.querySelector("[data-own-pill='not-for-publication']")).not.toBeNull()
	})
})
