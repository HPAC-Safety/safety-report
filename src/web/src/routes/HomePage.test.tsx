import { render, screen } from "@testing-library/react"
import { describe, expect, it, vi } from "vitest"
import { HomePage } from "./HomePage"

vi.mock("./HomePage.view", () => ({ HomePageView: () => <p>Home view</p> }))

describe("HomePage", () => {
	it("renders its view", () => {
		render(<HomePage />)

		expect(screen.getByText("Home view")).toBeTruthy()
	})
})
