import { describe, expect, it } from "vitest"
import { resolveInitialLocale } from "./resolveInitialLocale"

describe("resolveInitialLocale", () => {
	it("prefers a supported stored value", () => {
		expect(resolveInitialLocale("fr-CA", ["en-CA"], "safety.hpac.ca")).toBe("fr-CA")
	})

	it("ignores an unsupported stored value and uses the hostname", () => {
		expect(resolveInitialLocale("xx", ["en-CA"], "securite.acvl.ca")).toBe("fr-CA")
	})

	it("ignores an empty stored value", () => {
		expect(resolveInitialLocale("", ["fr-CA"], "localhost")).toBe("fr-CA")
	})

	it("uses an exactly supported browser language", () => {
		expect(resolveInitialLocale(null, ["de", "fr-CA"], "localhost")).toBe("fr-CA")
	})

	it("matches a browser language by its prefix", () => {
		expect(resolveInitialLocale(null, ["de-DE", "fr-FR"], "localhost")).toBe("fr-CA")
	})

	it("falls back to English", () => {
		expect(resolveInitialLocale(null, ["de-DE"], "localhost")).toBe("en-CA")
		expect(resolveInitialLocale(null, [], "localhost")).toBe("en-CA")
	})
})
