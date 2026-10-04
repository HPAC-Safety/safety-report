import type { Page } from "@playwright/test"

/*
 * The browser's own unload prompt (`beforeunload`), seen from outside: a When
 * tries to reload the tab and records each prompt it raised, dismissing it so
 * the page stays; a Then reads what was recorded.
 */
const prompts = new WeakMap<Page, string[]>()

/** Tries to reload the tab; a prompt that appears is recorded and dismissed, which cancels the reload. */
export function tryToReload(page: Page) {
	const shown: string[] = []
	prompts.set(page, shown)
	page.on("dialog", (dialog) => {
		shown.push(dialog.type())
		void dialog.dismiss()
	})
	void page.reload().catch(() => {}) // A prompt cancels the reload, so it may never complete.
}

/** The kinds of prompt the last attempt to reload raised. */
export function unloadPromptsShown(page: Page): string[] {
	return prompts.get(page) ?? []
}
