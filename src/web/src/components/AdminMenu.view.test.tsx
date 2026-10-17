import { cleanup, render, screen } from "@testing-library/react"
import { createRef } from "react"
import { MemoryRouter } from "react-router-dom"
import { afterEach, describe, expect, it, vi } from "vitest"
import { LocaleContext } from "../i18n/LocaleProvider"
import { AdminMenuView } from "./AdminMenu.view"

afterEach(cleanup)

function renderMenu(isAdministrator: boolean) {
	render(
		<LocaleContext.Provider value={{ locale: "en-CA", setLocale: () => {}, t: (key) => key }}>
			<MemoryRouter>
				<AdminMenuView
					open
					reports={0}
					typeAheadValues={0}
					isAdministrator={isAdministrator}
					containerRef={createRef()}
					buttonRef={createRef()}
					onToggle={vi.fn()}
					onSelectItem={vi.fn()}
				/>
			</MemoryRouter>
		</LocaleContext.Provider>,
	)
}

describe("AdminMenuView manage-roles link", () => {
	it("is offered to an Administrator", () => {
		renderMenu(true)

		expect(screen.getByRole("menuitem", { name: "nav.manageRoles" }).getAttribute("href")).toBe("/admin/roles")
	})

	it("is not offered to a Safety Officer", () => {
		renderMenu(false)

		expect(screen.queryByRole("menuitem", { name: "nav.manageRoles" })).toBeNull()
	})
})
