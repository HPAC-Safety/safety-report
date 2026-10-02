import { cleanup, render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"
import App from "./App"

vi.mock("./App.view", () => ({ AppView: () => <p>the app view</p> }))

afterEach(cleanup)

describe("App", () => {
	it("renders its view", () => {
		render(<App />)

		expect(screen.getByText("the app view")).toBeTruthy()
	})
})
