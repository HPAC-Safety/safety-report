import { cleanup, render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { Footer } from "./Footer"

vi.mock("./Footer.view", () => ({ FooterView: () => <p>the footer view</p> }))

afterEach(cleanup)

describe("Footer", () => {
	it("renders its view", () => {
		render(<Footer />)

		expect(screen.getByText("the footer view")).toBeTruthy()
	})
})
