import { useLocale } from "../i18n/useLocale"
import type { TranslationDirection } from "./translationDirection"

export interface TranslationDirectionSwitchViewProps {
	direction: TranslationDirection
	toFrench: boolean
	announcement: string
	onFlip: () => void
}

export function TranslationDirectionSwitchView({ direction, toFrench, announcement, onFlip }: TranslationDirectionSwitchViewProps) {
	const { t } = useLocale()
	const codeClassName = "absolute left-0 top-0 w-6 text-center transition-transform duration-200 motion-reduce:transition-none"

	return (
		<>
			<button
				type="button"
				className="touch-target inline-flex items-center rounded-full border border-rule bg-surface px-3 font-sans text-xs font-medium text-ink hover:bg-surface-2"
				aria-label={t(`translationDirection.${direction}`)}
				onClick={onFlip}
			>
				{/* The source code sits left of the arrow and the target right; a flip slides each to the other side. */}
				<span className="relative block h-4 w-[4.5rem]" aria-hidden="true">
					<span className={`${codeClassName} ${toFrench ? "translate-x-0" : "translate-x-12"}`}>
						{t("translationDirection.english")}
					</span>
					<span className="absolute left-6 top-0 w-6 text-center text-ink-muted">→</span>
					<span className={`${codeClassName} ${toFrench ? "translate-x-12" : "translate-x-0"}`}>
						{t("translationDirection.french")}
					</span>
				</span>
			</button>
			<span className="sr-only" aria-live="polite">
				{announcement}
			</span>
		</>
	)
}
