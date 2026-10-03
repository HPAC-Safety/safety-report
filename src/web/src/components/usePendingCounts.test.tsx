import { act, cleanup, renderHook, waitFor } from "@testing-library/react"
import type { ReactNode } from "react"
import { MemoryRouter, useNavigate } from "react-router-dom"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { getPendingCounts, type PendingCounts } from "../api/adminReports"
import { usePendingCounts } from "./usePendingCounts"

vi.mock("../api/adminReports", () => ({ getPendingCounts: vi.fn() }))

const counts: PendingCounts = { reportsNeedingAction: 3, answersAwaitingTranslation: null, typeAheadValuesAwaitingReview: 1 }
const wrapper = ({ children }: { children: ReactNode }) => <MemoryRouter>{children}</MemoryRouter>

describe("usePendingCounts", () => {
	beforeEach(() => {
		vi.mocked(getPendingCounts).mockReset()
	})
	afterEach(cleanup)

	it("fetches nothing while disabled", () => {
		const { result } = renderHook(() => usePendingCounts(false), { wrapper })
		expect(result.current).toBeNull()
		expect(getPendingCounts).not.toHaveBeenCalled()
	})

	it("loads the counts while enabled", async () => {
		vi.mocked(getPendingCounts).mockResolvedValue(counts)
		const { result } = renderHook(() => usePendingCounts(true), { wrapper })
		await waitFor(() => expect(result.current).toEqual(counts))
	})

	it("shows no count when the read fails", async () => {
		vi.mocked(getPendingCounts).mockRejectedValue(new Error("down"))
		const { result } = renderHook(() => usePendingCounts(true), { wrapper })
		await act(async () => {
			await Promise.resolve()
		})
		expect(result.current).toBeNull()
	})

	it("clears the counts when disabled after being enabled", async () => {
		vi.mocked(getPendingCounts).mockResolvedValue(counts)
		const { result, rerender } = renderHook(({ on }) => usePendingCounts(on), { wrapper, initialProps: { on: true } })
		await waitFor(() => expect(result.current).toEqual(counts))
		rerender({ on: false })
		await waitFor(() => expect(result.current).toBeNull())
	})

	it("ignores a stale success and a stale failure after unmount", async () => {
		let resolve: (value: PendingCounts) => void = () => {}
		let reject: (reason: Error) => void = () => {}
		vi.mocked(getPendingCounts)
			.mockReturnValueOnce(new Promise<PendingCounts>((r) => (resolve = r)))
			.mockReturnValueOnce(new Promise<PendingCounts>((_r, j) => (reject = j)))
		const first = renderHook(() => usePendingCounts(true), { wrapper })
		const second = renderHook(() => usePendingCounts(true), { wrapper })
		first.unmount()
		second.unmount()
		resolve(counts)
		reject(new Error("late"))
		await Promise.resolve()
		expect(first.result.current).toBeNull()
		expect(second.result.current).toBeNull()
	})

	it("refetches on navigation", async () => {
		vi.mocked(getPendingCounts).mockResolvedValue(counts)
		const { result } = renderHook(() => ({ counts: usePendingCounts(true), navigate: useNavigate() }), { wrapper })
		await waitFor(() => expect(getPendingCounts).toHaveBeenCalledTimes(1))
		void result.current.navigate("/elsewhere")
		await waitFor(() => expect(getPendingCounts).toHaveBeenCalledTimes(2))
	})
})
