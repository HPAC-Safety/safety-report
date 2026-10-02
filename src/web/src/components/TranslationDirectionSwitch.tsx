import { useState } from "react"
import { useLocale } from "../i18n/useLocale"
import {
	DEFAULT_TRANSLATION_DIRECTION,
	translationLocales,
	type TranslationDirection,
} from "./translationDirection"
import { TranslationDirectionSwitchView } from "./TranslationDirectionSwitch.view"

export { DEFAULT_TRANSLATION_DIRECTION, translationLocales, type TranslationDirection }

export interface TranslationDirectionSwitchProps {
	direction: TranslationDirection
	onChange: (direction: TranslationDirection) => void
}

/** The view model: the live-region announcement and the flip, which tells the caller the new direction. */
export function useTranslationDirectionSwitch({ direction, onChange }: TranslationDirectionSwitchProps) {
	const { t } = useLocale()
	// Empty until the administrator flips it, so opening a screen announces nothing.
	const [announcement, setAnnouncement] = useState("")
	const toFrench = direction === "toFrench"

	return {
		toFrench,
		announcement,
		onFlip: () => {
			const next = toFrench ? "toEnglish" : "toFrench"
			setAnnouncement(t(`translationDirection.${next}`))
			onChange(next)
		},
	}
}

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
export function TranslationDirectionSwitch(props: TranslationDirectionSwitchProps) {
	return <TranslationDirectionSwitchView {...props} {...useTranslationDirectionSwitch(props)} />
}
