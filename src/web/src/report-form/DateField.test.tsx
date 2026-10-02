import { act, cleanup, fireEvent, render, screen } from "@testing-library/react"
import { useState } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import type { Locale } from "../i18n/locales"
import { DateField } from "./DateField"

const t = (key: string, params?: Record<string, string | number>) => (params?.date ? `${key}:${params.date}` : key)

function Harness({
	initial = "",
	allowFutureDates = false,
	locale = "en-CA",
	placeholder,
	onValue,
}: {
	initial?: string
	allowFutureDates?: boolean
	locale?: Locale
	placeholder?: string
	onValue?: (value: string) => void
}) {
	const [value, setValue] = useState(initial)
	return (
		<div>
			<DateField
				fieldId="q"
				className="field"
				describedBy="d"
				placeholder={placeholder}
				value={value}
				allowFutureDates={allowFutureDates}
				locale={locale}
				onChange={(next) => {
					setValue(next)
					onValue?.(next)
				}}
				t={t}
			/>
			<button id="outside">outside</button>
		</div>
	)
}

const input = () => document.getElementById("q") as HTMLInputElement
const dialog = () => document.getElementById("q-calendar") as HTMLElement
const isOpen = () => !dialog().hidden
const day = (iso: string) => document.querySelector<HTMLButtonElement>(`button[data-day="${iso}"]`)
const status = () => document.querySelector("p[role='status']") as HTMLElement
const month = () => screen.getByLabelText("report.date.month") as HTMLSelectElement
const year = () => screen.getByLabelText("report.date.year") as HTMLSelectElement
const pressOn = (element: Element, key: string, init: KeyboardEventInit = {}) => fireEvent.keyDown(element, { key, ...init })
const openByClick = () => fireEvent.click(input())

beforeEach(() => {
	vi.useFakeTimers()
	vi.setSystemTime(new Date(2026, 5, 15, 12))
})

afterEach(() => {
	cleanup()
	vi.useRealTimers()
	Reflect.deleteProperty(window, "matchMedia")
})

describe("DateField on a touch device", () => {
	function touch(matches: boolean) {
		window.matchMedia = vi.fn(() => ({ matches }) as MediaQueryList)
	}

	it("uses the native date input, held to today where the future is not allowed", () => {
		touch(true)
		const onValue = vi.fn()
		render(<Harness initial="2026-01-02" onValue={onValue} />)

		expect(input().type).toBe("date")
		expect(input().max).toBe("2026-06-15")
		expect(input().value).toBe("2026-01-02")
		expect(input().getAttribute("aria-describedby")).toBe("d")
		fireEvent.change(input(), { target: { value: "2026-02-03" } })
		expect(onValue).toHaveBeenCalledWith("2026-02-03")
	})

	it("sets no limit where a future date is allowed", () => {
		touch(true)
		render(<Harness allowFutureDates />)

		expect(input().max).toBe("")
	})

	it("uses the calendar when the main pointer is not a finger", () => {
		touch(false)
		render(<Harness />)

		expect(input().type).toBe("text")
	})
})

describe("DateField calendar", () => {
	it("is a text field naming its format, with the calendar closed", () => {
		render(<Harness />)

		expect(input().type).toBe("text")
		expect(input().placeholder).toBe("report.date.placeholder")
		expect(input().getAttribute("aria-describedby")).toBe("q-format d")
		expect(document.getElementById("q-format")?.textContent).toBe("report.date.placeholder")
		expect(isOpen()).toBe(false)
		expect(dialog().getAttribute("aria-label")).toBe("report.date.calendar")
		expect(input().getAttribute("aria-expanded")).toBe("false")
	})

	it("shows the question's own placeholder, and names only the format when nothing describes it", () => {
		render(
			<DateField fieldId="q" className="field" describedBy={undefined} placeholder="Pick one" value="" allowFutureDates={false} locale="en-CA" onChange={() => {}} t={t} />,
		)

		expect(input().placeholder).toBe("Pick one")
		expect(input().getAttribute("aria-describedby")).toBe("q-format")
	})

	it("types a date straight into the field", () => {
		const onValue = vi.fn()
		render(<Harness onValue={onValue} />)

		fireEvent.change(input(), { target: { value: "2026-03-04" } })

		expect(onValue).toHaveBeenCalledWith("2026-03-04")
	})

	it("opens on focus and on click, on the chosen date's month or today's", () => {
		const { unmount } = render(<Harness />)
		act(() => input().focus())
		expect(isOpen()).toBe(true)
		expect(month().value).toBe("6")
		expect(year().value).toBe("2026")
		unmount()

		render(<Harness initial="2025-02-10" />)
		openByClick()
		expect(isOpen()).toBe(true)
		expect(month().value).toBe("2")
		expect(year().value).toBe("2025")
		expect(day("2025-02-10")?.closest("td")?.getAttribute("aria-selected")).toBe("true")
		expect(day("2025-02-11")?.closest("td")?.getAttribute("aria-selected")).toBe("false")
	})

	it("opens from an invalid typed value on today's month", () => {
		render(<Harness initial="not a date" />)

		openByClick()

		expect(month().value).toBe("6")
	})

	it("stays as it is when clicked again while open", () => {
		render(<Harness />)

		openByClick()
		fireEvent.click(month().parentElement as HTMLElement)
		openByClick()

		expect(isOpen()).toBe(true)
	})

	it("marks today, and blocks later days where the future is not allowed", () => {
		render(<Harness />)
		openByClick()

		expect(day("2026-06-15")?.getAttribute("aria-current")).toBe("date")
		expect(day("2026-06-14")?.getAttribute("aria-current")).toBeNull()
		expect(day("2026-06-16")?.getAttribute("aria-disabled")).toBe("true")
		expect(day("2026-06-16")?.className).toContain("cursor-not-allowed")
		expect(day("2026-06-14")?.getAttribute("aria-disabled")).toBeNull()
		expect(day("2026-06-14")?.className).not.toContain("cursor-not-allowed")
		expect(day("2026-06-15")?.className).toContain("border-brand-700")
		expect(day("2026-06-15")?.getAttribute("aria-label")).toContain("2026")
	})

	it("styles the chosen day", () => {
		render(<Harness initial="2026-06-10" />)
		openByClick()

		expect(day("2026-06-10")?.className).toContain("bg-brand-700")
	})

	it("does not choose a blocked day", () => {
		const onValue = vi.fn()
		render(<Harness onValue={onValue} />)
		openByClick()

		fireEvent.click(day("2026-06-16") as HTMLElement)

		expect(onValue).not.toHaveBeenCalled()
		expect(isOpen()).toBe(true)
	})

	it("chooses a day, returns focus to the field without reopening, and announces it after a moment", () => {
		const onValue = vi.fn()
		render(<Harness onValue={onValue} />)
		openByClick()

		fireEvent.click(day("2026-06-10") as HTMLElement)

		expect(onValue).toHaveBeenCalledWith("2026-06-10")
		expect(isOpen()).toBe(false)
		expect(document.activeElement).toBe(input())
		expect(status().textContent).toBe("")
		act(() => {
			vi.advanceTimersByTime(60)
		})
		expect(status().textContent).toContain("report.date.chosen:")
	})

	it("announces choosing the same day again by clearing first", () => {
		render(<Harness />)
		openByClick()
		fireEvent.click(day("2026-06-10") as HTMLElement)
		act(() => {
			vi.advanceTimersByTime(60)
		})

		openByClick()
		fireEvent.click(day("2026-06-10") as HTMLElement)
		expect(status().textContent).toBe("")
		act(() => {
			vi.advanceTimersByTime(60)
		})
		expect(status().textContent).not.toBe("")
	})

	it("opens again on the next focus after the quiet one", () => {
		render(<Harness />)
		openByClick()
		fireEvent.click(day("2026-06-10") as HTMLElement)
		expect(isOpen()).toBe(false)

		act(() => input().blur())
		act(() => input().focus())

		expect(isOpen()).toBe(true)
	})

	it("goes into the calendar with ArrowDown from the field, on the chosen day or today", () => {
		const { unmount } = render(<Harness initial="2026-06-10" />)
		pressOn(input(), "ArrowDown")
		expect(isOpen()).toBe(true)
		expect(document.activeElement).toBe(day("2026-06-10"))
		unmount()

		render(<Harness />)
		openByClick()
		pressOn(input(), "ArrowDown")
		expect(document.activeElement).toBe(day("2026-06-15"))
	})

	it("closes on Escape in the field without taking focus, only while open", () => {
		render(<Harness />)

		expect(fireEvent.keyDown(input(), { key: "Escape" })).toBe(true)
		openByClick()
		expect(fireEvent.keyDown(input(), { key: "Escape" })).toBe(false)
		expect(isOpen()).toBe(false)
		expect(fireEvent.keyDown(input(), { key: "x" })).toBe(true)
	})

	it("closes on Escape inside the calendar and gives focus back to the field", () => {
		const outer = vi.fn()
		render(
			// eslint-disable-next-line jsx-a11y/no-static-element-interactions -- a probe: it records whether Escape bubbles past the field to the page
			<div onKeyDown={outer}>
				<Harness />
			</div>,
		)
		openByClick()
		pressOn(input(), "ArrowDown")
		outer.mockClear()

		pressOn(document.activeElement as Element, "Escape")

		expect(isOpen()).toBe(false)
		expect(document.activeElement).toBe(input())
		expect(outer).not.toHaveBeenCalled()
	})

	it("ignores other keys inside the dialog", () => {
		render(<Harness />)
		openByClick()

		expect(fireEvent.keyDown(dialog(), { key: "x" })).toBe(true)
		expect(isOpen()).toBe(true)
	})

	it("moves the focused day with the arrow keys and the page keys", () => {
		render(<Harness initial="2026-06-10" />)
		pressOn(input(), "ArrowDown")
		const press = (key: string) => pressOn(document.activeElement as Element, key)

		press("ArrowLeft")
		expect(document.activeElement).toBe(day("2026-06-09"))
		press("ArrowRight")
		expect(document.activeElement).toBe(day("2026-06-10"))
		press("ArrowUp")
		expect(document.activeElement).toBe(day("2026-06-03"))
		press("ArrowDown")
		expect(document.activeElement).toBe(day("2026-06-10"))
		press("PageUp")
		expect(document.activeElement).toBe(day("2026-05-10"))
		expect(month().value).toBe("5")
		press("PageDown")
		expect(document.activeElement).toBe(day("2026-06-10"))
	})

	it("keeps the focused day on or before today where the future is not allowed", () => {
		render(<Harness initial="2026-06-14" />)
		pressOn(input(), "ArrowDown")

		pressOn(document.activeElement as Element, "ArrowDown")

		expect(document.activeElement).toBe(day("2026-06-15"))
	})

	it("may go to a future day where the question allows one", () => {
		render(<Harness initial="2026-06-14" allowFutureDates />)
		pressOn(input(), "ArrowDown")

		pressOn(document.activeElement as Element, "ArrowDown")

		expect(document.activeElement).toBe(day("2026-06-21"))
	})

	it("chooses the focused day on Enter or Space, and ignores other keys", () => {
		const onValue = vi.fn()
		render(<Harness initial="2026-06-10" onValue={onValue} />)
		pressOn(input(), "ArrowDown")

		expect(fireEvent.keyDown(document.activeElement as Element, { key: "x" })).toBe(true)
		pressOn(document.activeElement as Element, "Enter")
		expect(onValue).toHaveBeenLastCalledWith("2026-06-10")

		pressOn(input(), "ArrowDown")
		pressOn(document.activeElement as Element, "ArrowLeft")
		pressOn(document.activeElement as Element, " ")
		expect(onValue).toHaveBeenLastCalledWith("2026-06-09")
	})

	it("does nothing on a key in the grid before a day has focus", () => {
		render(<Harness />)
		openByClick()

		expect(fireEvent.keyDown(screen.getByRole("grid"), { key: "ArrowLeft" })).toBe(true)
	})

	it("puts the calendar's controls in the Tab order only once focus is inside", () => {
		render(<Harness initial="2026-06-10" />)
		openByClick()
		const previous = screen.getByLabelText("report.date.previousMonth")

		expect(previous.getAttribute("tabindex")).toBe("-1")
		expect(month().tabIndex).toBe(-1)
		expect(day("2026-06-10")?.tabIndex).toBe(-1)

		fireEvent.focus(dialog())

		expect(previous.getAttribute("tabindex")).toBe("0")
		expect(month().tabIndex).toBe(0)
		expect(year().tabIndex).toBe(0)
		expect(screen.getByLabelText("report.date.nextMonth").getAttribute("tabindex")).toBe("0")
		expect(day("2026-06-10")?.tabIndex).toBe(0)
		expect(day("2026-06-11")?.tabIndex).toBe(-1)
	})

	it("lands a Tab into the grid on the chosen day, today, or the first of the month", () => {
		render(<Harness initial="2026-06-10" />)
		openByClick()
		fireEvent.focus(dialog())
		expect(day("2026-06-10")?.tabIndex).toBe(0)

		fireEvent.click(screen.getByLabelText("report.date.previousMonth"))
		expect(day("2026-05-01")?.tabIndex).toBe(0)
		cleanup()

		render(<Harness />)
		openByClick()
		fireEvent.focus(dialog())
		expect(day("2026-06-15")?.tabIndex).toBe(0)
	})

	it("lands a Tab on today when the chosen date is in another month", () => {
		render(<Harness initial="2026-03-10" />)
		openByClick()
		fireEvent.focus(dialog())
		fireEvent.change(month(), { target: { value: "6" } })

		expect(day("2026-06-15")?.tabIndex).toBe(0)
	})

	it("steps and picks months and years", () => {
		render(<Harness initial="2026-03-10" />)
		openByClick()

		fireEvent.click(screen.getByLabelText("report.date.nextMonth"))
		expect(month().value).toBe("4")
		fireEvent.click(screen.getByLabelText("report.date.previousMonth"))
		fireEvent.click(screen.getByLabelText("report.date.previousMonth"))
		expect(month().value).toBe("2")
		fireEvent.change(month(), { target: { value: "11" } })
		expect(month().value).toBe("11")
		fireEvent.change(year(), { target: { value: "2020" } })
		expect(year().value).toBe("2020")
		expect(month().value).toBe("11")
	})

	it("blocks the next month and later months where the future is not allowed", () => {
		render(<Harness />)
		openByClick()

		expect((screen.getByLabelText("report.date.nextMonth") as HTMLButtonElement).disabled).toBe(true)
		expect(Array.from(month().options).filter((option) => option.disabled).map((option) => option.value)).toEqual(["7", "8", "9", "10", "11", "12"])
	})

	it("brings a year whose chosen month is still ahead down to today's month", () => {
		render(<Harness initial="2025-09-10" />)
		openByClick()

		fireEvent.change(year(), { target: { value: "2026" } })

		expect(year().value).toBe("2026")
		expect(month().value).toBe("6")
	})

	it("offers 100 years back and, where a future is allowed, 10 ahead", () => {
		const { unmount } = render(<Harness />)
		openByClick()
		let years = Array.from(year().options).map((option) => Number(option.value))
		expect(years[0]).toBe(2026)
		expect(years.at(-1)).toBe(1926)
		unmount()

		render(<Harness allowFutureDates />)
		openByClick()
		years = Array.from(year().options).map((option) => Number(option.value))
		expect(years[0]).toBe(2036)
		expect((screen.getByLabelText("report.date.nextMonth") as HTMLButtonElement).disabled).toBe(false)
	})

	it("widens the years to a typed date outside the usual range", () => {
		const { unmount } = render(<Harness initial="1900-05-05" />)
		openByClick()
		expect(Array.from(year().options).at(-1)?.value).toBe("1900")
		unmount()

		render(<Harness initial="2200-05-05" />)
		openByClick()
		expect(Array.from(year().options)[0].value).toBe("2200")
	})

	it("starts the week on Monday in French", () => {
		const { unmount } = render(<Harness locale="fr-CA" />)
		openByClick()
		const french = screen.getAllByRole("columnheader").map((header) => header.getAttribute("abbr"))
		unmount()

		render(<Harness />)
		openByClick()
		const english = screen.getAllByRole("columnheader").map((header) => header.getAttribute("abbr"))

		expect(english[0]).toMatch(/sunday/i)
		expect(french[0]).toMatch(/lundi/i)
	})

	it("heads the grid with the month and year, for assistive technology", () => {
		render(<Harness />)
		openByClick()

		const heading = document.getElementById(screen.getByRole("grid").getAttribute("aria-labelledby") as string)
		expect(heading?.textContent).toBe("June 2026")
	})

	it("closes once focus leaves both the field and the calendar", () => {
		render(<Harness />)
		openByClick()
		const wrapper = input().parentElement as HTMLElement

		fireEvent.blur(wrapper, { relatedTarget: day("2026-06-10") })
		expect(isOpen()).toBe(true)
		fireEvent.blur(wrapper, { relatedTarget: null })
		expect(isOpen()).toBe(false)
	})

	it("keeps focus where it is on a press on the calendar's background, but not on a control", () => {
		render(<Harness />)
		openByClick()

		expect(fireEvent.mouseDown(dialog())).toBe(false)
		expect(fireEvent.mouseDown(month())).toBe(true)
		expect(fireEvent.mouseDown(day("2026-06-10") as HTMLElement)).toBe(true)
	})

	it("cancels a pending announcement when it unmounts", () => {
		const { unmount } = render(<Harness />)
		openByClick()
		fireEvent.click(day("2026-06-10") as HTMLElement)

		unmount()

		expect(() => vi.advanceTimersByTime(100)).not.toThrow()
	})
})
