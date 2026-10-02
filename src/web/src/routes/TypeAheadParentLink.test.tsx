import { cleanup, fireEvent, render, renderHook, screen } from "@testing-library/react"
import type { ReactNode } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { LocaleContext } from "../i18n/LocaleProvider"
import type { Locale } from "../i18n/locales"
import { TypeAheadParentLink, useTypeAheadParentLink, type TypeAheadParentLinkProps } from "./TypeAheadParentLink"

vi.mock("../report-form/MultiSelectPicker", () => ({
	MultiSelectPicker: (props: { fieldId: string; label: string; values: string[]; locked: string[]; groups: { key: string; label: string }[][]; onToggle: (id: string) => void }) => (
		<div data-picker={props.fieldId} data-label={props.label} data-values={props.values.join()} data-locked={props.locked.join()} data-options={props.groups[0].map((o) => o.label).join()}>
			<button onClick={() => props.onToggle("c1")}>toggle c1</button>
			<button onClick={() => props.onToggle("c2")}>toggle c2</button>
		</div>
	),
}))

afterEach(cleanup)

const wrapperFor = (locale: Locale) =>
	function Wrapper({ children }: { children: ReactNode }) {
		return <LocaleContext.Provider value={{ locale, setLocale: () => {}, t: (key, params) => (params ? `${key} ${JSON.stringify(params)}` : key) }}>{children}</LocaleContext.Provider>
	}

const wording = (choice: { labelEn: string | null; labelFr: string | null }) => choice.labelEn ?? ""

function props(over: Partial<TypeAheadParentLinkProps> = {}): TypeAheadParentLinkProps {
	return {
		valueId: "v1",
		parent: {
			questionId: "q",
			questionLabelEn: "Aircraft type",
			questionLabelFr: "Type d'aéronef",
			parentChoiceIds: ["c1"],
			choices: [
				{ id: "c2", labelEn: "Zeta", labelFr: null, pin: "none" },
				{ id: "c1", labelEn: "Alpha", labelFr: null, pin: "none" },
			],
		},
		chosen: ["c1"],
		onChoose: vi.fn(),
		onRelink: vi.fn(),
		wording,
		...over,
	}
}

describe("useTypeAheadParentLink", () => {
	it("reads the question in the reader's language, sorts the choices and finds the current ones", () => {
		const en = renderHook(() => useTypeAheadParentLink(props()), { wrapper: wrapperFor("en-CA") })
		expect(en.result.current.question).toBe("Aircraft type")
		expect(en.result.current.sorted.map((c) => c.id)).toEqual(["c1", "c2"])
		expect(en.result.current.current.map((c) => c.id)).toEqual(["c1"])
		const fr = renderHook(() => useTypeAheadParentLink(props()), { wrapper: wrapperFor("fr-CA") })
		expect(fr.result.current.question).toBe("Type d'aéronef")
	})

	it("is unchanged only when the selection equals the current parents", () => {
		const same = renderHook(() => useTypeAheadParentLink(props()), { wrapper: wrapperFor("en-CA") })
		expect(same.result.current.unchanged).toBe(true)
		const longer = renderHook(() => useTypeAheadParentLink(props({ chosen: ["c1", "c2"] })), { wrapper: wrapperFor("en-CA") })
		expect(longer.result.current.unchanged).toBe(false)
		const other = renderHook(() => useTypeAheadParentLink(props({ chosen: ["c2"] })), { wrapper: wrapperFor("en-CA") })
		expect(other.result.current.unchanged).toBe(false)
	})
})

describe("TypeAheadParentLink", () => {
	it("says where the value is offered now and locks the last parent", () => {
		render(<TypeAheadParentLink {...props()} />, { wrapper: wrapperFor("en-CA") })
		expect(screen.getByTestId("type-ahead-value-parent").textContent).toBe('typeAheadValues.offeredUnder {"question":"Aircraft type","choice":"Alpha"}')
		const picker = document.querySelector("[data-picker]")!
		expect(picker.getAttribute("data-picker")).toBe("type-ahead-value-parent-v1")
		expect(picker.getAttribute("data-locked")).toBe("c1")
		expect(picker.getAttribute("data-options")).toBe("Alpha,Zeta")
		expect((screen.getByRole("button", { name: "typeAheadValues.relink" }) as HTMLButtonElement).disabled).toBe(true)
	})

	it("says when it is offered under nothing and does not lock when several are ticked", () => {
		render(<TypeAheadParentLink {...props({ parent: { ...props().parent, parentChoiceIds: [] }, chosen: ["c1", "c2"] })} />, { wrapper: wrapperFor("en-CA") })
		expect(screen.getByTestId("type-ahead-value-parent").textContent).toBe('typeAheadValues.offeredUnderNothing {"question":"Aircraft type"}')
		expect(document.querySelector("[data-picker]")!.getAttribute("data-locked")).toBe("")
	})

	it("ticks and unticks through onChoose and relinks the selection", () => {
		const onChoose = vi.fn()
		const onRelink = vi.fn()
		render(<TypeAheadParentLink {...props({ chosen: ["c1", "c2"], onChoose, onRelink })} />, { wrapper: wrapperFor("en-CA") })
		fireEvent.click(screen.getByText("toggle c1"))
		expect(onChoose).toHaveBeenLastCalledWith(["c2"])
		cleanup()
		render(<TypeAheadParentLink {...props({ chosen: ["c1"], onChoose, onRelink })} />, { wrapper: wrapperFor("en-CA") })
		fireEvent.click(screen.getByText("toggle c2"))
		expect(onChoose).toHaveBeenLastCalledWith(["c1", "c2"])
		cleanup()
		render(<TypeAheadParentLink {...props({ chosen: ["c2"], onChoose, onRelink })} />, { wrapper: wrapperFor("en-CA") })
		fireEvent.click(screen.getByRole("button", { name: "typeAheadValues.relink" }))
		expect(onRelink).toHaveBeenCalledWith(["c2"])
	})

	it("cannot relink with nothing chosen", () => {
		render(<TypeAheadParentLink {...props({ chosen: [] })} />, { wrapper: wrapperFor("en-CA") })
		expect((screen.getByRole("button", { name: "typeAheadValues.relink" }) as HTMLButtonElement).disabled).toBe(true)
	})
})
