import { cleanup, render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { ReportFormErrorBoundary } from "./ReportFormErrorBoundary"

afterEach(cleanup)

function Broken(): never {
	throw new Error("render failed")
}

describe("ReportFormErrorBoundary", () => {
	it("renders its children while they render", () => {
		render(
			<ReportFormErrorBoundary t={(key) => key}>
				<p>the form</p>
			</ReportFormErrorBoundary>,
		)

		expect(screen.getByText("the form")).toBeTruthy()
	})

	it("replaces a form that fails to render with an alert, and touches no storage", () => {
		const error = vi.spyOn(console, "error").mockImplementation(() => {})
		// React rethrows the render error to the window; the boundary has handled it.
		const quiet = (event: ErrorEvent) => event.preventDefault()
		window.addEventListener("error", quiet)
		localStorage.setItem("kept", "answer")

		render(
			<ReportFormErrorBoundary t={(key) => `t:${key}`}>
				<Broken />
			</ReportFormErrorBoundary>,
		)

		expect(screen.getByRole("alert").textContent).toBe("t:report.loadError")
		expect(localStorage.getItem("kept")).toBe("answer")
		window.removeEventListener("error", quiet)
		error.mockRestore()
	})
})
