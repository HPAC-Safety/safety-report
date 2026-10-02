import { render, screen } from "@testing-library/react"
import { describe, expect, it, vi } from "vitest"
import { NotFoundPage } from "./NotFoundPage"

vi.mock("./NotFoundPage.view", () => ({ NotFoundPageView: () => <p>NotFound view</p> }))

describe("NotFoundPage", () => {
	it("renders its view", () => {
		render(<NotFoundPage />)

		expect(screen.getByText("NotFound view")).toBeTruthy()
	})
})
