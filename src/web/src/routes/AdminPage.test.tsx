import { render, screen } from "@testing-library/react"
import { describe, expect, it, vi } from "vitest"
import { AdminPage } from "./AdminPage"

vi.mock("./AdminPage.view", () => ({ AdminPageView: () => <p>Admin view</p> }))

describe("AdminPage", () => {
	it("renders its view", () => {
		render(<AdminPage />)

		expect(screen.getByText("Admin view")).toBeTruthy()
	})
})
