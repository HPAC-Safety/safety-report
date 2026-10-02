import { describe, expect, it } from "vitest"
import { DEFAULT_TRANSLATION_DIRECTION, translationLocales } from "./translationDirection"

describe("translationDirection", () => {
	it("starts English to French", () => {
		expect(DEFAULT_TRANSLATION_DIRECTION).toBe("toFrench")
	})

	it("names the locales a direction translates from and to", () => {
		expect(translationLocales("toFrench")).toEqual({ from: "en-CA", to: "fr-CA" })
		expect(translationLocales("toEnglish")).toEqual({ from: "fr-CA", to: "en-CA" })
	})
})
