import { act, fireEvent, render, renderHook, screen, cleanup } from "@testing-library/react"
import type { DragEndEvent } from "@dnd-kit/core"
import { afterEach, describe, expect, it, vi } from "vitest"
import { SortableList, useSortableList } from "./SortableList"
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

type Item = { id: string }
const items: Item[] = [{ id: "a" }, { id: "b" }, { id: "c" }]
const drag = (active: string, over: string | null) => ({ active: { id: active }, over: over === null ? null : { id: over } }) as DragEndEvent

function hook(onReorder = vi.fn()) {
	const { result } = renderHook(() => useSortableList({ items, getId: (item) => item.id, onReorder, children: () => null, label: "L" }))
	return { result, onReorder }
}

describe("useSortableList", () => {
	it("exposes the ids in order", () => {
		expect(hook().result.current.ids).toEqual(["a", "b", "c"])
	})

	it("moves a row and reports the new order", () => {
		const { result, onReorder } = hook()
		result.current.move(0, 2)
		expect(onReorder).toHaveBeenCalledWith(["b", "c", "a"])
	})

	it("ignores a move off either end", () => {
		const { result, onReorder } = hook()
		result.current.move(0, -1)
		result.current.move(2, 3)
		expect(onReorder).not.toHaveBeenCalled()
	})

	it("reorders on a drop over another row", () => {
		const { result, onReorder } = hook()
		result.current.onDragEnd(drag("c", "a"))
		expect(onReorder).toHaveBeenCalledWith(["c", "a", "b"])
	})

	it("ignores a drop over nothing or over itself", () => {
		const { result, onReorder } = hook()
		result.current.onDragEnd(drag("a", null))
		result.current.onDragEnd(drag("a", "a"))
		expect(onReorder).not.toHaveBeenCalled()
	})
})

describe("SortableList", () => {
	it("renders each item in a row with move buttons that reorder", () => {
		const onReorder = vi.fn()
		render(
			<SortableList items={items} getId={(item) => item.id} onReorder={onReorder} label="Things">
				{(item, index) => <span>{`row ${item.id} ${index}`}</span>}
			</SortableList>,
			{ wrapper },
		)
		expect(screen.getByRole("list", { name: "Things" })).toBeTruthy()
		expect(screen.getByText("row b 1")).toBeTruthy()
		expect((screen.getAllByRole("button", { name: "questions.moveUp" })[0] as HTMLButtonElement).disabled).toBe(true)
		act(() => {
			fireEvent.click(screen.getAllByRole("button", { name: "questions.moveDown" })[0])
		})
		expect(onReorder).toHaveBeenCalledWith(["b", "a", "c"])
	})
})
