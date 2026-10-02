import { Component, type ReactNode } from "react"
import type { LocaleContextValue } from "../i18n/LocaleProvider"
import { ReportFormErrorBoundaryView } from "./ReportFormErrorBoundary.view"

export interface ReportFormErrorBoundaryProps {
	t: LocaleContextValue["t"]
	children: ReactNode
}

interface State {
	failed: boolean
}

/**
 * Catches a render error in the form itself. Deliberately does nothing to
 * `localStorage` — the reporter's saved answers must survive a script error
 * exactly as they would survive closing the tab, and nothing here is a
 * candidate answer to publish, so there is nothing to expose either.
 */
export class ReportFormErrorBoundary extends Component<ReportFormErrorBoundaryProps, State> {
	state: State = { failed: false }

	static getDerivedStateFromError(): State {
		return { failed: true }
	}

	render() {
		if (this.state.failed) {
			return <ReportFormErrorBoundaryView t={this.props.t} />
		}

		return this.props.children
	}
}
