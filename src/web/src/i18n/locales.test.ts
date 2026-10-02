import { describe, expect, it } from "vitest"
import { DEFAULT_LOCALE, STORAGE_KEY, SUPPORTED_LOCALES, isSupportedLocale, localeForHostname } from "./locales"

describe("locales", () => {
	it("lists the supported locales and defaults to English", () => {
		expect(SUPPORTED_LOCALES).toEqual(["en-CA", "fr-CA"])
		expect(DEFAULT_LOCALE).toBe("en-CA")
		expect(STORAGE_KEY).toBe("hpac.locale")
	})

	it("recognises supported locales only", () => {
		expect(isSupportedLocale("fr-CA")).toBe(true)
		expect(isSupportedLocale("de-DE")).toBe(false)
	})

	it("maps production hostnames to a locale", () => {
		expect(localeForHostname("safety.hpac.ca")).toBe("en-CA")
		expect(localeForHostname("securite.acvl.ca")).toBe("fr-CA")
	})

	it("returns null for an unlisted hostname", () => {
		expect(localeForHostname("localhost")).toBeNull()
	})
})
