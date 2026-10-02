import { AppView } from "./App.view"

// A layout route (ADR-0043): `main.tsx` builds the data router `useBlocker`
// needs (issue no. 659) from `routes.tsx`'s route config, with this component as
// its root and each page rendered into `Outlet`.
//
// `UnsavedChangesGuardRoot` wraps everything in the view, once: React Router
// allows only one active `useBlocker` per router, so it owns the one blocker
// (and the one confirm dialog) every form's `useUnsavedChangesGuard` registers
// with, instead of each form calling `useBlocker` itself (issue no. 659).
function App() {
	return <AppView />
}

export default App
