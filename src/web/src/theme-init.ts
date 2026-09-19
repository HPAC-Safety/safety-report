import { STORAGE_KEY } from "./theme/resolveInitialTheme"

// Imported as the first line of main.tsx (side-effect import, so it runs to
// completion before anything else in that module, including render()) —
// not a separate <script> tag: two HTML entry points get merged into one
// bundle in a build-dependent order, which broke the "runs before React
// mounts" guarantee this relies on. A single-entry import order is
// well-defined instead. See ADR-0052.
//
// There is no flash of the wrong theme because of when this runs, not
// because of anything client-visible: absent/invalid storage leaves
// data-theme unset and prefers-color-scheme governs, per ADR-0024.
try {
	const storedTheme = localStorage.getItem(STORAGE_KEY)
	if (storedTheme === "light" || storedTheme === "dark") {
		document.documentElement.dataset.theme = storedTheme
	}
} catch {
	// Storage unavailable; fall through to prefers-color-scheme.
}
