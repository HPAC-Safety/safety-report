import { render, screen, cleanup } from "@testing-library/react"
import { afterEach, describe, expect, it } from "vitest"
import { CountBadge } from "./CountBadge"
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

describe("CountBadge", () => {
	it("renders nothing for zero, null and undefined", () => {
		for (const count of [0, null, undefined]) {
			const { container, unmount } = render(<CountBadge count={count} />, { wrapper })
			expect(container.innerHTML).toBe("")
			unmount()
		}
	})

	it("shows the count with a screen-reader label", () => {
		const { container } = render(<CountBadge count={3} />, { wrapper })
		expect(container.querySelector("[data-count-badge='3']")).not.toBeNull()
		expect(screen.getByText("3")).toBeTruthy()
		expect(container.textContent).toContain('nav.pendingCount {"count":"3"}')
	})

	it("caps the visible count at 99+", () => {
		render(<CountBadge count={150} />, { wrapper })
		expect(screen.getByText("99+")).toBeTruthy()
	})
})
