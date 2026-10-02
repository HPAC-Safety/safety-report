import { render, screen } from "@testing-library/react"
import { describe, expect, it, vi } from "vitest"
import { PlaceholderPage } from "./PlaceholderPage"

vi.mock("./PlaceholderPage.view", () => ({ PlaceholderPageView: ({ pageName }: { pageName: string }) => <p>view of {pageName}</p> }))

describe("PlaceholderPage", () => {
	it("passes its page name to its view", () => {
		render(<PlaceholderPage pageName="Stats" />)

		expect(screen.getByText("view of Stats")).toBeTruthy()
	})
})
