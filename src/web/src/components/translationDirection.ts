export type TranslationDirection = "toFrench" | "toEnglish"

export const DEFAULT_TRANSLATION_DIRECTION: TranslationDirection = "toFrench"

/** The locale codes a direction translates from and to, as the translate endpoint names them. */
export function translationLocales(direction: TranslationDirection): { from: string; to: string } {
	return direction === "toFrench" ? { from: "en-CA", to: "fr-CA" } : { from: "fr-CA", to: "en-CA" }
}
