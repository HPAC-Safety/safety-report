import { beforeEach, describe, expect, it, vi } from "vitest"

beforeEach(() => {
	vi.resetModules()
	vi.doUnmock("../../../../locales/en-CA.json")
	vi.doUnmock("../../../../locales/fr-CA.json")
})

describe("catalogueFor", () => {
	it("returns the flattened English catalogue", async () => {
		const { catalogueFor } = await import("./catalogueFor")
		const english = catalogueFor("en-CA")
		expect(Object.keys(english).length).toBeGreaterThan(0)
		for (const value of Object.values(english)) expect(typeof value).toBe("string")
		expect(Object.keys(english).some((key) => key.includes("."))).toBe(true)
	})

	it("returns the same catalogue on a second call", async () => {
		const { catalogueFor } = await import("./catalogueFor")
		expect(catalogueFor("en-CA")).toBe(catalogueFor("en-CA"))
		expect(catalogueFor("fr-CA")).toBe(catalogueFor("fr-CA"))
	})

	it("merges the French catalogue over English", async () => {
		const { catalogueFor } = await import("./catalogueFor")
		const english = catalogueFor("en-CA")
		const french = catalogueFor("fr-CA")
		expect(Object.keys(french)).toEqual(expect.arrayContaining(Object.keys(english)))
		expect(french["app.title"]).not.toBe(english["app.title"])
	})

	it("flattens nested objects, skipping non-string leaves", async () => {
		vi.doMock("../../../../locales/en-CA.json", () => ({
			default: { a: { b: "x" }, c: "y", d: null, e: 5 },
		}))
		const { catalogueFor } = await import("./catalogueFor")
		expect(catalogueFor("en-CA")).toEqual({ "a.b": "x", c: "y" })
	})

	it("falls back to English for a key French lacks", async () => {
		vi.doMock("../../../../locales/en-CA.json", () => ({ default: { k: "english", j: "also english" } }))
		vi.doMock("../../../../locales/fr-CA.json", () => ({ default: { j: "français" } }))
		const { catalogueFor } = await import("./catalogueFor")
		expect(catalogueFor("fr-CA")).toEqual({ k: "english", j: "français" })
	})

	it("falls back to English for a locale with no file", async () => {
		const { catalogueFor } = await import("./catalogueFor")
		const english = catalogueFor("en-CA")
		expect(catalogueFor("xx-XX" as unknown as "fr-CA")).toEqual(english)
	})

	it("never bundles the other files in the locales directory", async () => {
		const { catalogueFor } = await import("./catalogueFor")
		const english = catalogueFor("en-CA")
		for (const name of ["fr-CA.meta", "glossary", "terms"]) {
			expect(catalogueFor(name as unknown as "fr-CA")).toEqual(english)
		}
	})
})
