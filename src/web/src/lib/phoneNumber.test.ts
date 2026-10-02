import { afterEach, describe, expect, it, vi } from "vitest"

import {
	callingCodeOf,
	DEFAULT_PHONE_COUNTRY,
	flagOf,
	formatStoredPhone,
	isTooLong,
	isValidPhone,
	maskPhone,
	phoneCountries,
	phonePlaceholder,
	readTypedPhone,
	toE164,
} from "./phoneNumber"

const stub = vi.hoisted(() => ({ noExample: false, oddInternational: false }))

// Delegates to the real library; a test flips a flag to reach a fallback the real data never triggers.
vi.mock("libphonenumber-js/max", async (importOriginal) => {
	const original = await importOriginal<typeof import("libphonenumber-js/max")>()
	class StubbedAsYouType extends original.AsYouType {
		constructor(...args: ConstructorParameters<typeof original.AsYouType>) {
			super(...args)
			this.hasCountry = args.length > 0
		}
		private hasCountry: boolean
		override input(text: string): string {
			return stub.oddInternational && !this.hasCountry ? "unexpected" : super.input(text)
		}
	}
	return {
		...original,
		AsYouType: StubbedAsYouType,
		getExampleNumber: (...args: Parameters<typeof original.getExampleNumber>) => (stub.noExample ? undefined : original.getExampleNumber(...args)),
	}
})

afterEach(() => {
	stub.noExample = false
	stub.oddInternational = false
	vi.restoreAllMocks()
})

describe("DEFAULT_PHONE_COUNTRY", () => {
	it("is Canada", () => {
		expect(DEFAULT_PHONE_COUNTRY).toBe("CA")
	})
})

describe("flagOf", () => {
	it("spells a region with regional-indicator symbols, ignoring case", () => {
		expect(flagOf("CA")).toBe("\u{1F1E8}\u{1F1E6}")
		expect(flagOf("ca")).toBe("\u{1F1E8}\u{1F1E6}")
	})
})

describe("callingCodeOf", () => {
	it("returns a known country's calling code", () => {
		expect(callingCodeOf("GB")).toBe("44")
	})

	it("falls back to the default country for an unknown or empty code", () => {
		expect(callingCodeOf("ZZ")).toBe("1")
		expect(callingCodeOf("")).toBe("1")
	})
})

describe("phoneCountries", () => {
	it("lists every country sorted by its localized name", () => {
		const en = phoneCountries("en-CA")
		expect(en.length).toBeGreaterThan(200)
		expect(en.find((country) => country.code === "CA")).toMatchObject({ name: "Canada", callingCode: "1", flag: flagOf("CA") })
		const names = en.map((country) => country.name)
		expect(names).toEqual([...names].sort(new Intl.Collator("en-CA").compare))
	})

	it("falls back to the region code when no name is known", () => {
		vi.spyOn(Intl, "DisplayNames").mockImplementation(function () {
			return { of: () => undefined } as unknown as Intl.DisplayNames
		} as unknown as typeof Intl.DisplayNames)
		expect(phoneCountries("en-CA").find((country) => country.code === "CA")?.name).toBe("CA")
	})

	it("names countries in French", () => {
		expect(phoneCountries("fr-CA").find((country) => country.code === "DE")?.name).toBe("Allemagne")
	})
})

describe("maskPhone", () => {
	it("returns an empty string for no digits", () => {
		expect(maskPhone("CA", "")).toBe("")
	})

	it("formats nationally where the country does", () => {
		expect(maskPhone("CA", "6045551234")).toBe("(604) 555-1234")
	})

	it("falls back to the international grouping without the calling code", () => {
		expect(maskPhone("GB", "2079460018")).toBe("20 7946 0018")
	})

	it("falls back to the default country for an unknown code", () => {
		expect(maskPhone("ZZ", "6045551234")).toBe("(604) 555-1234")
	})

	it("returns the digits when the international form lacks the calling code prefix", () => {
		stub.oddInternational = true
		expect(maskPhone("GB", "2079460018")).toBe("2079460018")
	})
})

describe("phonePlaceholder", () => {
	it("shows the pattern of the example number with 5 for each digit", () => {
		expect(phonePlaceholder("CA")).toBe("(555) 555-5555")
	})

	it("returns an empty string for a country with no example number", () => {
		stub.noExample = true
		expect(phonePlaceholder("CA")).toBe("")
	})
})

describe("isTooLong", () => {
	it("is true only when one more digit would exceed the country's length", () => {
		expect(isTooLong("CA", "60455512345678")).toBe(true)
		expect(isTooLong("CA", "6045551234")).toBe(false)
		expect(isTooLong("CA", "604")).toBe(false)
	})
})

describe("readTypedPhone", () => {
	it("keeps the country and strips non-digits when there is no plus", () => {
		expect(readTypedPhone("CA", "(604) 555-1234")).toEqual({ country: "CA", digits: "6045551234" })
	})

	it("reads the country named by a leading plus", () => {
		expect(readTypedPhone("CA", " +44 20 7946 0018")).toEqual({ country: "GB", digits: "2079460018" })
	})

	it("keeps the typed country when the plus names none", () => {
		expect(readTypedPhone("CA", "+")).toEqual({ country: "CA", digits: "" })
		expect(readTypedPhone("CA", "+999")).toEqual({ country: "CA", digits: "999" })
	})
})

describe("isValidPhone", () => {
	it("validates against the country's rules", () => {
		expect(isValidPhone("CA", "6045551234")).toBe(true)
		expect(isValidPhone("CA", "123")).toBe(false)
	})
})

describe("toE164", () => {
	it("returns the E.164 form", () => {
		expect(toE164("CA", "(604) 555-1234")).toBe("+16045551234")
	})

	it("returns null when the number does not parse", () => {
		expect(toE164("CA", "abc")).toBeNull()
	})
})

describe("formatStoredPhone", () => {
	it("groups an E.164 number internationally", () => {
		expect(formatStoredPhone("+16045551234")).toBe("+1 604 555 1234")
	})

	it("returns a value without a plus as stored", () => {
		expect(formatStoredPhone("6045551234")).toBe("6045551234")
	})

	it("returns an unparsable plus value as stored", () => {
		expect(formatStoredPhone("+abc")).toBe("+abc")
	})
})
