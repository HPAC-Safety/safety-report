import { beforeEach, describe, expect, it, vi } from "vitest"

beforeEach(() => {
	vi.resetModules()
	vi.doUnmock("../../../../locales/en-CA.json")
})

describe("loadCatalogue", () => {
	it("returns the flattened English catalogue", async () => {
		const { loadCatalogue } = await import("./loadCatalogue")
		const english = await loadCatalogue("en-CA")
		expect(Object.keys(english).length).toBeGreaterThan(0)
		for (const value of Object.values(english)) expect(typeof value).toBe("string")
		expect(Object.keys(english).some((key) => key.includes("."))).toBe(true)
	})

	it("returns the cached English catalogue on a second call", async () => {
		const { loadCatalogue } = await import("./loadCatalogue")
		expect(await loadCatalogue("en-CA")).toBe(await loadCatalogue("en-CA"))
	})

	it("merges a translated catalogue over English", async () => {
		const { loadCatalogue } = await import("./loadCatalogue")
		const english = await loadCatalogue("en-CA")
		const french = await loadCatalogue("fr-CA")
		expect(Object.keys(french).sort()).toEqual(expect.arrayContaining(Object.keys(english)))
	})

	it("flattens nested objects, skipping non-string leaves", async () => {
		vi.doMock("../../../../locales/en-CA.json", () => ({
			default: { a: { b: "x" }, c: "y", d: null, e: 5 },
		}))
		const { loadCatalogue } = await import("./loadCatalogue")
		expect(await loadCatalogue("en-CA")).toEqual({ "a.b": "x", c: "y" })
	})

	it("falls back to English when a locale file fails to load", async () => {
		vi.doMock("../../../../locales/en-CA.json", () => ({ default: { k: "english" } }))
		vi.doMock("../../../../locales/fr-CA.json", () => {
			throw new Error("boom")
		})
		const { loadCatalogue } = await import("./loadCatalogue")
		expect(await loadCatalogue("fr-CA")).toEqual({ k: "english" })
		vi.doUnmock("../../../../locales/fr-CA.json")
	})

	it("returns an empty catalogue when English fails to load", async () => {
		vi.doMock("../../../../locales/en-CA.json", () => {
			throw new Error("boom")
		})
		const { loadCatalogue } = await import("./loadCatalogue")
		expect(await loadCatalogue("en-CA")).toEqual({})
	})

	it("falls back to English for a locale with no file", async () => {
		const { loadCatalogue } = await import("./loadCatalogue")
		const english = await loadCatalogue("en-CA")
		const missing = await loadCatalogue("xx-XX" as unknown as "fr-CA")
		expect(missing).toEqual(english)
	})

	it("treats every other JSON file in the locales directory as a loadable catalogue", async () => {
		const { loadCatalogue } = await import("./loadCatalogue")
		for (const name of ["fr-CA.meta", "glossary", "terms"]) {
			const merged = await loadCatalogue(name as unknown as "fr-CA")
			expect(typeof merged).toBe("object")
		}
	})
})
