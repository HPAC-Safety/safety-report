export type Theme = "light" | "dark"

export const STORAGE_KEY = "hpac.theme"

function isTheme(value: string | null): value is Theme {
	return value === "light" || value === "dark"
}

/**
 * `null` means no explicit override: `data-theme` stays unset and
 * `prefers-color-scheme` governs, per ADR-0024. Pure and DOM-free so it can
 * be exercised without a browser.
 */
export function resolveInitialTheme(storedValue: string | null): Theme | null {
	return isTheme(storedValue) ? storedValue : null
}
