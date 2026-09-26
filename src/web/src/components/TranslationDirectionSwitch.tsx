import { useState } from "react"
import { useLocale } from "../i18n/useLocale"

/*
 * The one control that says which way Translate goes: English to French, or
 * French to English. The administrator picks it; nothing infers it from which
 * field happens to be empty (ADR-0141).
 *
 * A compact pill rather than a select: it shows the two language codes with an
 * arrow between them, and one press swaps them. It is a real button, so Space
 * and Enter flip it too; its accessible name states the current direction, and
 * a polite live region announces each change. The swap slides the two codes
 * past each other, and not at all under reduced motion.
 *
 * Controlled: the caller holds the direction. Every screen starts English to
 * French (`DEFAULT_TRANSLATION_DIRECTION`).
 */

export type TranslationDirection = "toFrench" | "toEnglish"

export const DEFAULT_TRANSLATION_DIRECTION: TranslationDirection = "toFrench"

/** The locale codes a direction translates from and to, as the translate endpoint names them. */
export function translationLocales(direction: TranslationDirection): { from: string; to: string } {
	return direction === "toFrench" ? { from: "en-CA", to: "fr-CA" } : { from: "fr-CA", to: "en-CA" }
}

export function TranslationDirectionSwitch({
	direction,
	onChange,
}: {
	direction: TranslationDirection
	onChange: (direction: TranslationDirection) => void
}) {
	const { t } = useLocale()
	// Empty until the administrator flips it, so opening a screen announces nothing.
	const [announcement, setAnnouncement] = useState("")
	const toFrench = direction === "toFrench"
	const codeClassName = "absolute left-0 top-0 w-6 text-center transition-transform duration-200 motion-reduce:transition-none"

	return (
		<>
			<button
				type="button"
				className="touch-target inline-flex items-center rounded-full border border-rule bg-surface px-3 font-sans text-xs font-medium text-ink hover:bg-surface-2"
				aria-label={t(`translationDirection.${direction}`)}
				onClick={() => {
					const next = toFrench ? "toEnglish" : "toFrench"
					setAnnouncement(t(`translationDirection.${next}`))
					onChange(next)
				}}
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
