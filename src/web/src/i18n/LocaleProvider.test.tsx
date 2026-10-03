import { act, cleanup, render, renderHook, screen, waitFor } from "@testing-library/react"
import { useContext } from "react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { DEFAULT_LOCALE as exportedDefault, LocaleContext, LocaleProvider, useLocaleProvider } from "./LocaleProvider"
import { DEFAULT_LOCALE, STORAGE_KEY } from "./locales"

const catalogueFor = vi.fn<(locale: string) => Record<string, string>>()
vi.mock("./catalogueFor", () => ({ catalogueFor: (locale: string) => catalogueFor(locale) }))

beforeEach(() => {
	localStorage.clear()
	document.title = "untitled"
	catalogueFor.mockImplementation((locale: string) =>
		locale === "fr-CA" ? { "app.title": "Titre", hello: "Bonjour {name} {missing}" } : { "app.title": "Title", hello: "Hello {name} {missing}" },
	)
})

afterEach(() => {
	cleanup()
	catalogueFor.mockReset()
	vi.restoreAllMocks()
	vi.unstubAllGlobals()
})

describe("useLocaleProvider", () => {
	it("starts from a stored choice", async () => {
		localStorage.setItem(STORAGE_KEY, "fr-CA")

		const { result } = renderHook(() => useLocaleProvider())

		expect(result.current.locale).toBe("fr-CA")
		await waitFor(() => expect(document.title).toBe("Titre"))
		expect(document.documentElement.lang).toBe("fr-CA")
	})

	it("falls back to the browser's language when none is stored", () => {
		vi.stubGlobal("navigator", { languages: ["fr-CA"], language: "fr-CA" })

		const { result } = renderHook(() => useLocaleProvider())

		expect(result.current.locale).toBe("fr-CA")
	})

	it("falls back to the browser's single language when it lists none", () => {
		vi.stubGlobal("navigator", { languages: undefined, language: "fr-CA" })

		const { result } = renderHook(() => useLocaleProvider())

		expect(result.current.locale).toBe("fr-CA")
	})

	it("starts without a stored locale when storage cannot be read", () => {
		vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
			throw new Error("blocked")
		})

		const { result } = renderHook(() => useLocaleProvider())

		expect(result.current.locale).toBe(DEFAULT_LOCALE)
	})

	it("translates a key, interpolating its parameters", async () => {
		const { result } = renderHook(() => useLocaleProvider())
		await waitFor(() => expect(document.title).toBe("Title"))

		expect(result.current.t("hello", { name: "Ada" })).toBe("Hello Ada {missing}")
		expect(result.current.t("hello")).toBe("Hello {name} {missing}")
		expect(result.current.t("no.such.key")).toBe("no.such.key")
	})

	it("keeps the document title when the catalogue has none", async () => {
		catalogueFor.mockReturnValue({})
		renderHook(() => useLocaleProvider())

		await act(async () => {})

		expect(document.title).toBe("untitled")
	})

	it("switches language and remembers the choice", async () => {
		const { result } = renderHook(() => useLocaleProvider())

		act(() => result.current.setLocale("fr-CA"))

		expect(result.current.locale).toBe("fr-CA")
		expect(localStorage.getItem(STORAGE_KEY)).toBe("fr-CA")
		await waitFor(() => expect(document.title).toBe("Titre"))
	})

	it("switches language even when storage cannot be written", async () => {
		vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
			throw new Error("quota")
		})
		const { result } = renderHook(() => useLocaleProvider())

		act(() => result.current.setLocale("fr-CA"))

		expect(result.current.locale).toBe("fr-CA")
		await waitFor(() => expect(document.title).toBe("Titre"))
	})

	it("translates on the first render, with no wait for a catalogue", () => {
		const { result } = renderHook(() => useLocaleProvider())

		expect(result.current.t("hello", { name: "Ada" })).toBe("Hello Ada {missing}")
	})

	it("still exports the default locale", () => {
		expect(exportedDefault).toBe(DEFAULT_LOCALE)
	})
})

describe("LocaleProvider", () => {
	function Reader() {
		const value = useContext(LocaleContext)
		return <p>{value?.t("hello", { name: "Ada" })}</p>
	}

	it("provides the context to its children", async () => {
		render(
			<LocaleProvider>
				<Reader />
			</LocaleProvider>,
		)

		await waitFor(() => expect(screen.getByText("Hello Ada {missing}")).toBeTruthy())
	})
})
