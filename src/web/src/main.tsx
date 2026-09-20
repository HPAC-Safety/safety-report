import "./theme-init"
import { StrictMode } from "react"
import { createRoot } from "react-dom/client"
import { BrowserRouter } from "react-router-dom"
import App from "./App"
import { LocaleProvider } from "./i18n/LocaleProvider"
import { ThemeProvider } from "./theme/ThemeProvider"
import { AuthProvider } from "./auth/AuthContext"
import "./index.css"

createRoot(document.getElementById("root")!).render(
	<StrictMode>
		<ThemeProvider>
			<LocaleProvider>
				<AuthProvider>
					<BrowserRouter>
						<App />
					</BrowserRouter>
				</AuthProvider>
			</LocaleProvider>
		</ThemeProvider>
	</StrictMode>,
)
