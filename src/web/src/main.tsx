import "./theme-init"
import { StrictMode } from "react"
import { createRoot } from "react-dom/client"
import { RouterProvider, createBrowserRouter } from "react-router-dom"
import { routes } from "./routes"
import { LocaleProvider } from "./i18n/LocaleProvider"
import { ThemeProvider } from "./theme/ThemeProvider"
import { AuthProvider } from "./auth/AuthContext"
import "./index.css"

// A data router, not a plain `<BrowserRouter>` (issue no. 659): only a data
// router supports `useBlocker`, which `useUnsavedChangesGuard` needs to pause
// an in-app route change away from a form with unsaved changes.
const router = createBrowserRouter(routes)

createRoot(document.getElementById("root")!).render(
	<StrictMode>
		<ThemeProvider>
			<LocaleProvider>
				<AuthProvider>
					<RouterProvider router={router} />
				</AuthProvider>
			</LocaleProvider>
		</ThemeProvider>
	</StrictMode>,
)
