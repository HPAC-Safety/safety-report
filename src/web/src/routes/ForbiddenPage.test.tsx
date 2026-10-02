import { render, screen } from "@testing-library/react"
import { describe, expect, it, vi } from "vitest"
import { ForbiddenPage } from "./ForbiddenPage"

vi.mock("./ForbiddenPage.view", () => ({ ForbiddenPageView: () => <p>Forbidden view</p> }))

describe("ForbiddenPage", () => {
	it("renders its view", () => {
		render(<ForbiddenPage />)

		expect(screen.getByText("Forbidden view")).toBeTruthy()
	})
})
