import { fireEvent, render, screen, cleanup } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { InfiniteScrollStatus } from "./InfiniteScrollStatus"
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

function renderStatus(overrides: Partial<Parameters<typeof InfiniteScrollStatus>[0]> = {}) {
	const onLoadMore = vi.fn()
	const sentinelRef = vi.fn()
	const result = render(
		<InfiniteScrollStatus hasMore={true} loadingMore={false} failed={false} onLoadMore={onLoadMore} sentinelRef={sentinelRef} announcement="Loaded 5" {...overrides} />,
		{ wrapper },
	)
	return { ...result, onLoadMore, sentinelRef }
}

describe("InfiniteScrollStatus", () => {
	it("shows the sentinel, the announcement and a Load more control while more pages remain", () => {
		const { container, sentinelRef, onLoadMore } = renderStatus()
		expect(container.querySelector("[data-infinite-scroll-sentinel]")).not.toBeNull()
		expect(sentinelRef).toHaveBeenCalled()
		expect(container.querySelector("[aria-live='polite']")?.textContent).toBe("Loaded 5")
		fireEvent.click(screen.getByRole("button", { name: "list.loadMore" }))
		expect(onLoadMore).toHaveBeenCalledTimes(1)
	})

	it("says it is loading while a page is on its way, and disables the control", () => {
		renderStatus({ loadingMore: true })
		expect((screen.getByRole("button", { name: "list.loadingMore" }) as HTMLButtonElement).disabled).toBe(true)
	})

	it("shows an alert and Retry, without the sentinel, after a failure", () => {
		const { container, onLoadMore } = renderStatus({ failed: true })
		expect(container.querySelector("[data-infinite-scroll-sentinel]")).toBeNull()
		expect(screen.getByRole("alert").textContent).toBe("list.error")
		fireEvent.click(screen.getByRole("button", { name: "list.retry" }))
		expect(onLoadMore).toHaveBeenCalledTimes(1)
	})

	it("shows no control once everything has loaded", () => {
		const { container } = renderStatus({ hasMore: false })
		expect(container.querySelector("button")).toBeNull()
		expect(container.querySelector("[data-infinite-scroll-sentinel]")).toBeNull()
	})
})
