import type { ReactNode } from "react"
import { LocaleContext, type LocaleContextValue } from "./localeContext"

export interface LocaleProviderViewProps {
	value: LocaleContextValue
	children: ReactNode
}

export function LocaleProviderView({ value, children }: LocaleProviderViewProps) {
	return <LocaleContext.Provider value={value}>{children}</LocaleContext.Provider>
}
