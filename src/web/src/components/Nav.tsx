import { NavView, type NavViewProps } from "./Nav.view"

const rowClassName = ({ isActive }: { isActive: boolean }) =>
	`touch-target inline-flex items-center rounded px-2 font-sans text-sm font-medium underline-offset-4 ${
		isActive ? "text-brand-700 underline" : "text-ink hover:underline"
	}`

const stackedClassName = ({ isActive }: { isActive: boolean }) =>
	`touch-target flex items-center rounded px-2 font-sans text-base font-medium underline-offset-4 ${
		isActive ? "text-brand-700 underline" : "text-ink hover:underline"
	}`

export interface NavProps {
	stacked?: boolean
	onNavigate?: () => void
}

export function useNav({ stacked = false }: NavProps): Pick<NavViewProps, "navClassName" | "linkClassName"> {
	return {
		navClassName: stacked ? "flex flex-col" : "flex flex-nowrap items-center gap-1",
		linkClassName: stacked ? stackedClassName : rowClassName,
	}
}

export function Nav(props: NavProps) {
	return <NavView onNavigate={props.onNavigate} {...useNav(props)} />
}
