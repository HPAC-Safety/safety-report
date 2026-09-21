import { Route, Routes } from "react-router-dom"
import { Header } from "./components/Header"
import { Footer } from "./components/Footer"
import { HomePage } from "./routes/HomePage"
import { ViewReportsPage } from "./routes/ViewReportsPage"
import { SubmitReportPage } from "./routes/SubmitReportPage"
import { ContactPage } from "./routes/ContactPage"
import { MemberLoginPage } from "./routes/MemberLoginPage"
import { AdminPage } from "./routes/AdminPage"
import { ManageReportsPage } from "./routes/ManageReportsPage"
import { ManageQuestionsPage } from "./routes/ManageQuestionsPage"
import { ManageChoiceListsPage } from "./routes/ManageChoiceListsPage"
import { NotFoundPage } from "./routes/NotFoundPage"

function App() {
	return (
		<div className="flex min-h-screen flex-col">
			<Header />
			<div className="flex-1">
				<Routes>
					<Route path="/" element={<HomePage />} />
					<Route path="/reports" element={<ViewReportsPage />} />
					<Route path="/report" element={<SubmitReportPage />} />
					<Route path="/contact" element={<ContactPage />} />
					<Route path="/login" element={<MemberLoginPage />} />
					<Route path="/admin" element={<AdminPage />} />
					<Route path="/admin/reports" element={<ManageReportsPage />} />
					<Route path="/admin/questions" element={<ManageQuestionsPage />} />
					<Route path="/admin/choice-lists" element={<ManageChoiceListsPage />} />
					<Route path="*" element={<NotFoundPage />} />
				</Routes>
			</div>
			<Footer />
		</div>
	)
}

export default App
