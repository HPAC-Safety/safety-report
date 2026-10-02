import { render, screen, cleanup } from "@testing-library/react"
import { afterEach, describe, expect, it } from "vitest"
import { AttachmentCountBadge } from "./AttachmentCountBadge"
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

describe("AttachmentCountBadge", () => {
	it("renders nothing at zero", () => {
		const { container } = render(<AttachmentCountBadge count={0} />, { wrapper })
		expect(container.innerHTML).toBe("")
	})

	it("labels a single attachment in the singular", () => {
		const { container } = render(<AttachmentCountBadge count={1} />, { wrapper })
		expect(screen.getByText('attachments.count.one {"count":"1"}')).toBeTruthy()
		expect(container.querySelector("[data-attachment-count='1'] svg[aria-hidden='true']")).not.toBeNull()
	})

	it("labels several attachments in the plural", () => {
		render(<AttachmentCountBadge count={4} />, { wrapper })
		expect(screen.getByText('attachments.count.other {"count":"4"}')).toBeTruthy()
	})
})
