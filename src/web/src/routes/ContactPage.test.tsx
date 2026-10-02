import { render, screen } from "@testing-library/react"
import { describe, expect, it, vi } from "vitest"
import { ContactPage } from "./ContactPage"

vi.mock("./ContactPage.view", () => ({ ContactPageView: () => <p>Contact view</p> }))

describe("ContactPage", () => {
	it("renders its view", () => {
		render(<ContactPage />)

		expect(screen.getByText("Contact view")).toBeTruthy()
	})
})
