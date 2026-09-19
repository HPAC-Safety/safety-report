import { Route, Routes } from "react-router-dom"
import { Header } from "./components/Header"
import { Footer } from "./components/Footer"
import { HomePage } from "./routes/HomePage"
import { ViewReportsPage } from "./routes/ViewReportsPage"
import { SubmitReportPage } from "./routes/SubmitReportPage"
import { ContactPage } from "./routes/ContactPage"
import { MemberLoginPage } from "./routes/MemberLoginPage"
import { AdminPage } from "./routes/AdminPage"
import { NotFoundPage } from "./routes/NotFoundPage"

function App() {
	return (
		<>
			<Header />
			<Routes>
				<Route path="/" element={<HomePage />} />
				<Route path="/reports" element={<ViewReportsPage />} />
				<Route path="/report" element={<SubmitReportPage />} />
				<Route path="/contact" element={<ContactPage />} />
				<Route path="/login" element={<MemberLoginPage />} />
				<Route path="/admin" element={<AdminPage />} />
				<Route path="*" element={<NotFoundPage />} />
			</Routes>
			<Footer />
		</>
	)
}

export default App
