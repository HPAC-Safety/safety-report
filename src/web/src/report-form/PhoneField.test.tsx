import { cleanup, fireEvent, render } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"
import type { DraftAnswer } from "./draft"
import { PhoneField } from "./PhoneField"

afterEach(cleanup)

function renderPhone(answer?: DraftAnswer) {
	const onChange = vi.fn()
	render(<PhoneField fieldId="q" describedBy="d" answer={answer} onChange={onChange} locale="en-CA" t={(key) => key} />)
	return {
		onChange,
		phone: document.getElementById("q") as HTMLInputElement,
		country: document.getElementById("q-country") as HTMLSelectElement,
		shown: document.getElementById("q-country-shown") as HTMLElement,
	}
}

describe("PhoneField", () => {
	it("starts on Canada with an empty, masked placeholder", () => {
		const { phone, country, shown } = renderPhone()

		expect(country.value).toBe("CA")
		expect(shown.textContent).toContain("+1")
		expect(phone.value).toBe("")
		expect(phone.placeholder).toBe("(555) 555-5555")
		expect(country.getAttribute("aria-label")).toBe("report.phone.country")
	})

	it("shows the answer's own country and number", () => {
		const { phone, country } = renderPhone({ kind: "value", value: "20 7946 0018", country: "GB" })

		expect(country.value).toBe("GB")
		expect(phone.value).toBe("20 7946 0018")
	})

	it("treats a non-value answer as empty", () => {
		const { phone } = renderPhone({ kind: "options", values: [] })

		expect(phone.value).toBe("")
	})

	it("masks the digits typed", () => {
		const { phone, onChange } = renderPhone()

		fireEvent.change(phone, { target: { value: "6045551234" } })

		expect(onChange).toHaveBeenCalledWith({ kind: "value", value: "(604) 555-1234", country: "CA" })
	})

	it("clears the answer when the field is emptied on Canada", () => {
		const { phone, onChange } = renderPhone({ kind: "value", value: "6", country: "CA" })

		fireEvent.change(phone, { target: { value: "" } })

		expect(onChange).toHaveBeenCalledWith(undefined)
	})

	it("deletes the digit before a deleted mask character", () => {
		const { phone, onChange } = renderPhone({ kind: "value", value: "(604) 555-1234", country: "CA" })

		fireEvent.change(phone, { target: { value: "(604 555-1234" } })

		expect(onChange).toHaveBeenCalledWith({ kind: "value", value: "(604) 555-123", country: "CA" })
	})

	it("keeps a shorter number when a digit itself was deleted", () => {
		const { phone, onChange } = renderPhone({ kind: "value", value: "(604) 555-1234", country: "CA" })

		fireEvent.change(phone, { target: { value: "(604) 555-123" } })

		expect(onChange).toHaveBeenCalledWith({ kind: "value", value: "(604) 555-123", country: "CA" })
	})

	it("refuses a digit past the country's longest number", () => {
		const { phone, onChange } = renderPhone({ kind: "value", value: "(604) 555-1234", country: "CA" })

		fireEvent.change(phone, { target: { value: "(604) 555-12345" } })

		expect(onChange).not.toHaveBeenCalled()
	})

	it("reads a number typed with a leading plus as its own country", () => {
		const { phone, onChange } = renderPhone()

		fireEvent.change(phone, { target: { value: "+442079460018" } })

		expect(onChange).toHaveBeenCalledWith({ kind: "value", value: "20 7946 0018", country: "GB" })
	})

	it("keeps a chosen country other than Canada even before a digit is typed", () => {
		const { country, onChange } = renderPhone()

		fireEvent.change(country, { target: { value: "GB" } })

		expect(onChange).toHaveBeenCalledWith({ kind: "value", value: "", country: "GB" })
	})

	it("re-masks the digits for a newly chosen country, and clears when back on an empty Canada", () => {
		const { country, onChange } = renderPhone({ kind: "value", value: "(604) 555-1234", country: "CA" })
		fireEvent.change(country, { target: { value: "GB" } })
		expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ kind: "value", country: "GB" }))
		cleanup()

		const empty = renderPhone({ kind: "value", value: "", country: "GB" })
		fireEvent.change(empty.country, { target: { value: "CA" } })
		expect(empty.onChange).toHaveBeenCalledWith(undefined)
	})
})
