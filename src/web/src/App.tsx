import { Route, Routes } from "react-router-dom"
import { Header } from "./components/Header"
import { Footer } from "./components/Footer"
import { AdminRouteGuard } from "./components/AdminRouteGuard"
import { HomePage } from "./routes/HomePage"
import { ViewReportsPage } from "./routes/ViewReportsPage"
import { PublicReportPage } from "./routes/PublicReportPage"
import { SubmitReportPage } from "./routes/SubmitReportPage"
import { ContactPage } from "./routes/ContactPage"
import { MemberLoginPage } from "./routes/MemberLoginPage"
import { AdminPage } from "./routes/AdminPage"
import { ManageReportsPage } from "./routes/ManageReportsPage"
import { ReportDetailPage } from "./routes/ReportDetailPage"
import { ManageQuestionsPage } from "./routes/ManageQuestionsPage"
import { ManageAnswerTranslationsPage } from "./routes/ManageAnswerTranslationsPage"
import { NotFoundPage } from "./routes/NotFoundPage"

function App() {
	return (
		<div className="flex min-h-screen flex-col">
			<Header />
			<div className="flex-1">
				<Routes>
					<Route path="/" element={<HomePage />} />
					<Route path="/reports" element={<ViewReportsPage />} />
					<Route path="/reports/:reportId" element={<PublicReportPage />} />
					{/* One optional-segment route, not two, so moving between pages never remounts the form. */}
					<Route path="/report/:stepKey?" element={<SubmitReportPage />} />
					<Route path="/contact" element={<ContactPage />} />
					<Route path="/login" element={<MemberLoginPage />} />
					<Route
						path="/admin"
						element={
							<AdminRouteGuard requires="reviewer">
								<AdminPage />
							</AdminRouteGuard>
						}
					/>
					<Route
						path="/admin/reports"
						element={
							<AdminRouteGuard requires="reviewer">
								<ManageReportsPage />
							</AdminRouteGuard>
						}
					/>
					<Route
						path="/admin/reports/:reportId"
						element={
							<AdminRouteGuard requires="reviewer">
								<ReportDetailPage />
							</AdminRouteGuard>
						}
					/>
					<Route
						path="/admin/questions"
						element={
							<AdminRouteGuard requires="administrator">
								<ManageQuestionsPage />
							</AdminRouteGuard>
						}
					/>
					<Route
						path="/admin/answer-translations"
						element={
							<AdminRouteGuard requires="administrator">
								<ManageAnswerTranslationsPage />
							</AdminRouteGuard>
						}
					/>
					<Route path="*" element={<NotFoundPage />} />
				</Routes>
			</div>
			<Footer />
		</div>
	)
}

export default App
