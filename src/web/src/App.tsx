import { Outlet } from "react-router-dom"
import { Header } from "./components/Header"
import { Footer } from "./components/Footer"
import { ScrollToTopOnNavigation } from "./components/ScrollToTopOnNavigation"
import { UnsavedChangesGuardRoot } from "./hooks/useUnsavedChangesGuard"

// A layout route (ADR-0043): `main.tsx` builds the data router `useBlocker`
// needs (issue no. 659) from `routes.tsx`'s route config, with this component as
// its root and each page rendered into `Outlet`.
//
// `UnsavedChangesGuardRoot` wraps everything here, once: React Router allows
// only one active `useBlocker` per router, so it owns the one blocker (and
// the one confirm dialog) every form's `useUnsavedChangesGuard` registers
// with, instead of each form calling `useBlocker` itself (issue no. 659).
function App() {
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

export default App
