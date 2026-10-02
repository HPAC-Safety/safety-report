import { fireEvent, render, renderHook, screen, cleanup } from "@testing-library/react"
import { DndContext } from "@dnd-kit/core"
import { SortableContext } from "@dnd-kit/sortable"
import { afterEach, describe, expect, it, vi } from "vitest"
import { SortableRow, useSortableRow } from "./SortableRow"
import type { ReactNode } from "react"
import { LocaleContext, type LocaleContextValue } from "../i18n/LocaleProvider"

afterEach(cleanup)

const value: LocaleContextValue = {
	locale: "en-CA",
	setLocale: () => {},
	t: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key),
}

function inList({ children }: { children: ReactNode }) {
	return (
		<LocaleContext.Provider value={value}>
			<DndContext>
				<SortableContext items={["x"]}>
					<ul>{children}</ul>
				</SortableContext>
			</DndContext>
		</LocaleContext.Provider>
	)
}

describe("useSortableRow", () => {
	it("returns the sortable refs, handle props and an idle style", () => {
		const { result } = renderHook(() => useSortableRow({ id: "x", position: 0, count: 1, onMove: vi.fn(), children: null }), { wrapper: inList })
		expect(typeof result.current.setNodeRef).toBe("function")
		expect(result.current.isDragging).toBe(false)
		expect(result.current.attributes.role).toBe("button")
		expect(result.current.style.transform).toBeUndefined()
	})
})

describe("SortableRow", () => {
	it("renders its content and moves by position", () => {
		const onMove = vi.fn()
		render(<SortableRow id="x" position={1} count={3} onMove={onMove}>content</SortableRow>, { wrapper: inList })
		expect(screen.getByText("content")).toBeTruthy()
		fireEvent.click(screen.getByRole("button", { name: "questions.moveUp" }))
		expect(onMove).toHaveBeenCalledWith(1, 0)
		fireEvent.click(screen.getByRole("button", { name: "questions.moveDown" }))
		expect(onMove).toHaveBeenCalledWith(1, 2)
		expect(screen.getByRole("button", { name: "questions.dragHandle" })).toBeTruthy()
	})

	it("disables move-up on the first row and move-down on the last", () => {
		render(<SortableRow id="x" position={0} count={1} onMove={vi.fn()}>c</SortableRow>, { wrapper: inList })
		expect((screen.getByRole("button", { name: "questions.moveUp" }) as HTMLButtonElement).disabled).toBe(true)
		expect((screen.getByRole("button", { name: "questions.moveDown" }) as HTMLButtonElement).disabled).toBe(true)
	})
})
