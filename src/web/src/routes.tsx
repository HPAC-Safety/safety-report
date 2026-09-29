import { Route, createRoutesFromElements } from "react-router-dom"
import App from "./App"
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
import { ReviewTypeAheadValuesPage } from "./routes/ReviewTypeAheadValuesPage"
import { NotFoundPage } from "./routes/NotFoundPage"

// Built from the same JSX Route tree the app used under `<Routes>`, so a data
// router (`createBrowserRouter` in `main.tsx`) can give `useUnsavedChangesGuard`
// (issue #659) a real `useBlocker` for in-app route changes — a plain
// `<BrowserRouter>` cannot support it.
export const routes = createRoutesFromElements(
	<Route element={<App />}>
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
			path="/admin/type-ahead-values"
			element={
				<AdminRouteGuard requires="reviewer">
					<ReviewTypeAheadValuesPage />
				</AdminRouteGuard>
			}
		/>
		<Route path="*" element={<NotFoundPage />} />
	</Route>,
)
