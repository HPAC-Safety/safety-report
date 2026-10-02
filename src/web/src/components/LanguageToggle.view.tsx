function GlobeIcon() {
	return (
		<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
			<circle cx="12" cy="12" r="9" />
			<path d="M3 12h18M12 3c2.5 2.5 3.75 5.5 3.75 9S14.5 20.5 12 21c-2.5-.5-3.75-5.5-3.75-9S9.5 5.5 12 3Z" />
		</svg>
	)
}

export interface LanguageToggleViewProps {
	/** The two-letter code of the current language, shown on the globe. */
	code: string
	/** The accessible name: switch to the other language. */
	label: string
	onToggle: () => void
}

export function LanguageToggleView({ code, label, onToggle }: LanguageToggleViewProps) {
	return (
		<button
			type="button"
			onClick={onToggle}
			aria-label={label}
			className="touch-target inline-flex items-center justify-center rounded text-ink-muted hover:text-ink"
		>
			<span className="relative inline-flex h-5 w-5 items-center justify-center">
				<GlobeIcon />
				<span aria-hidden="true" className="absolute -bottom-1.5 -right-1.5 rounded-sm bg-surface font-sans text-[9px] font-semibold leading-none text-ink">
					{code}
				</span>
			</span>
		</button>
	)
}
