import { Outlet } from "react-router-dom"
import { Header } from "./components/Header"
import { Footer } from "./components/Footer"
import { ScrollToTopOnNavigation } from "./components/ScrollToTopOnNavigation"
import { UnsavedChangesGuardRoot } from "./hooks/UnsavedChangesGuardRoot"

export function AppView() {
	return (
		<UnsavedChangesGuardRoot>
			<ScrollToTopOnNavigation />
			<div className="flex min-h-screen flex-col">
				<Header />
				<div className="flex-1">
					<Outlet />
				</div>
				<Footer />
			</div>
		</UnsavedChangesGuardRoot>
	)
}
