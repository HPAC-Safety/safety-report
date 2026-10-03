import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import type { PublicOptionView, PublicQuestionView } from "../api/publicQuestions"
import type { DraftAnswer } from "./draft"
import { QuestionField, type QuestionFieldProps } from "./QuestionField"

vi.mock("../api/uploads", async (importOriginal) => ({
	...(await importOriginal<typeof import("../api/uploads")>()),
	uploadAttachment: vi.fn(),
	deleteUpload: vi.fn(async () => {}),
}))

const option = (id: string, labelEn: string, labelFr: string, extra: Partial<PublicOptionView> = {}): PublicOptionView => ({
	id,
	code: id,
	labelEn,
	labelFr,
	onlyIn: null,
	pin: "none",
	aliases: [],
	...extra,
})

const options = [option("o-b", "Banana", "Banane"), option("o-a", "Apple", "Pomme", { onlyIn: "en", aliases: [{ labelEn: "Pomme?", labelFr: null }] })]

function question(type: string, extra: Partial<PublicQuestionView> = {}): PublicQuestionView {
	return {
		id: `id-${type}`,
		key: type,
		role: "none",
		revisionId: "rev1",
		type,
		isRequired: false,
		isPrivate: false,
		allowFutureDates: false,
		displayOrder: 1,
		dependsOnQuestionId: null,
		dependsOnChoiceId: null,
		allowsReporterAdditions: false,
		labelEn: "Label",
		labelFr: "Étiquette",
		helpTextEn: null,
		helpTextFr: null,
		placeholderEn: null,
		placeholderFr: null,
		options: [],
		children: [],
		...extra,
	}
}

const t = (key: string) => key

function renderField(type: string, overrides: Partial<QuestionFieldProps> = {}, extra: Partial<PublicQuestionView> = {}) {
	const onChange = vi.fn<(answer: DraftAnswer | undefined) => void>()
	const view = render(
		<QuestionField
			question={question(type, extra)}
			locale="en-CA"
			answer={undefined}
			onChange={onChange}
			attachments={[]}
			onAttachmentsChange={() => {}}
			onUploadingChange={() => {}}
			attachmentRoom={5}
			errorText={null}
			t={t}
			{...overrides}
		/>,
	)
	return { ...view, onChange }
}

const field = () => document.getElementById("question-rev1") as HTMLElement

beforeEach(() => {
	Element.prototype.scrollIntoView = vi.fn()
})

afterEach(cleanup)

describe("QuestionField", () => {
	it("draws a yes/no question as a pair of radios named for the question, with a legend", () => {
		const { onChange } = renderField("yes_no", { answer: { kind: "value", value: "no" } }, { isRequired: true })

		expect(screen.getByText("Label:", { exact: false }).tagName).toBe("LEGEND")
		expect(screen.getByText("report.required.badge")).toBeTruthy()
		const [yes, no] = screen.getAllByRole<HTMLInputElement>("radio")
		expect(yes.checked).toBe(false)
		expect(no.checked).toBe(true)
		expect(yes.name).toBe("question-rev1")
		fireEvent.click(yes)

		expect(onChange).toHaveBeenCalledWith({ kind: "value", value: "yes" })
		expect(screen.getByText("report.booleanYes")).toBeTruthy()
		expect(screen.getByText("report.booleanNo")).toBeTruthy()
	})

	it("draws a checkbox question like a yes/no one, and reads an unanswered one as neither", () => {
		renderField("checkbox", { answer: { kind: "options", values: [] } })

		expect(screen.getAllByRole<HTMLInputElement>("radio").map((radio) => radio.checked)).toEqual([false, false])
	})

	it("describes the question by its note, help, and error, in that order", () => {
		renderField("short_text", { errorText: "Required", note: "Answer the parent first", announcement: "Choices changed" }, { helpTextEn: "Some help" })

		expect(field().getAttribute("aria-describedby")).toBe("question-rev1-note question-rev1-help question-rev1-error")
		expect(document.getElementById("question-rev1-help")?.textContent).toBe("Some help")
		expect(screen.getByRole("alert").textContent).toBe("Required")
		expect(field().previousElementSibling?.tagName).toBe("LABEL")
	})

	it("describes the question by nothing when there is nothing to say", () => {
		renderField("short_text")

		expect(field().getAttribute("aria-describedby")).toBeNull()
		expect(screen.queryByRole("alert")).toBeNull()
	})

	it("draws a type-ahead with its note and live announcement", () => {
		renderField(
			"autocomplete",
			{ note: "Pick the parent first", announcement: "Choices changed", disabled: true },
			{ options, labelEn: "City?" },
		)

		expect(screen.getByTestId("question-note").textContent).toBe("Pick the parent first")
		expect(screen.getByTestId("question-announcement").textContent).toBe("Choices changed")
		expect(screen.getByRole<HTMLInputElement>("combobox").disabled).toBe(true)
		expect(screen.getByText("City?").tagName).toBe("LABEL")
	})

	it("draws a type-ahead with no note or announcement by default", () => {
		renderField("autocomplete", {}, { options, placeholderEn: "Type a city" })

		expect(screen.queryByTestId("question-note")).toBeNull()
		expect(screen.queryByTestId("question-announcement")).toBeNull()
		expect(screen.getByRole<HTMLInputElement>("combobox").placeholder).toBe("Type a city")
	})

	it("answers a type-ahead with the typed text, or clears it when emptied", () => {
		const { onChange } = renderField("autocomplete", { answer: { kind: "value", value: "Ban" } }, { options })
		const input = screen.getByRole("combobox")

		fireEvent.change(input, { target: { value: "Bana" } })
		expect(onChange).toHaveBeenLastCalledWith({ kind: "value", value: "Bana" })
		fireEvent.change(input, { target: { value: "" } })
		expect(onChange).toHaveBeenLastCalledWith(undefined)
	})

	it("answers a type-ahead with the choice picked from its list, shown in the reader's language", () => {
		const { onChange, rerender } = renderField("autocomplete", { answer: { kind: "value", value: "Ban" } }, { options })

		fireEvent.click(screen.getByRole("combobox"))
		fireEvent.click(document.getElementById("question-rev1-option-o-b") as HTMLElement)
		expect(onChange).toHaveBeenLastCalledWith({ kind: "value", value: "Banana", choice: "o-b" })

		rerender(
			<QuestionField
				question={question("autocomplete", { options })}
				locale="fr-CA"
				answer={{ kind: "value", value: "Banana", choice: "o-b" }}
				onChange={onChange}
				attachments={[]}
				onAttachmentsChange={() => {}}
				onUploadingChange={() => {}}
				attachmentRoom={5}
				errorText={null}
				t={t}
			/>,
		)
		expect(screen.getByRole<HTMLInputElement>("combobox").value).toBe("Banane")
	})

	it("falls back to the typed text when the held choice is no longer offered", () => {
		renderField("autocomplete", { answer: { kind: "value", value: "Gone", choice: "o-missing" } }, { options })

		expect(screen.getByRole<HTMLInputElement>("combobox").value).toBe("Gone")
	})

	it("draws a single-select with a placeholder, and answers with the choice's ID or clears it", () => {
		const { onChange } = renderField("single_select", {}, { options })

		expect(screen.getByRole("combobox").textContent).toBe("report.select.placeholder")
		fireEvent.click(screen.getByRole("combobox"), { detail: 1 })
		fireEvent.click(document.getElementById("question-rev1-option-o-a") as HTMLElement)
		expect(onChange).toHaveBeenLastCalledWith({ kind: "value", value: "o-a" })
		fireEvent.click(screen.getByRole("combobox"), { detail: 1 })
		fireEvent.click(document.getElementById("question-rev1-option-none") as HTMLElement)
		expect(onChange).toHaveBeenLastCalledWith(undefined)
	})

	it("finds a single-select's choice from an older draft's label", () => {
		renderField("single_select", { answer: { kind: "value", value: "Banane" } }, { options })

		expect(screen.getByRole("combobox").textContent).toBe("Banana")
	})

	it("draws a multi-select with the prompt and badge in its label, and toggles choices by ID", () => {
		const { onChange } = renderField("multi_select", { answer: { kind: "options", values: ["Pomme", "gone"] } }, { options, isRequired: true })

		expect(screen.getByText("report.required.badge")).toBeTruthy()
		expect(document.getElementById("question-rev1-summary")?.textContent).toBe("Apple")
		fireEvent.click(document.getElementById("question-rev1") as HTMLElement)
		const [apple, banana] = screen.getAllByRole<HTMLInputElement>("checkbox")
		expect(apple.checked).toBe(true)
		fireEvent.click(banana)
		expect(onChange).toHaveBeenLastCalledWith({ kind: "options", values: ["o-a", "gone", "o-b"] })
		fireEvent.click(apple)
		expect(onChange).toHaveBeenLastCalledWith({ kind: "options", values: ["gone"] })
	})

	it("clears a multi-select's answer when its last choice is unticked", () => {
		const { onChange } = renderField("multi_select", { answer: { kind: "options", values: ["o-a"] } }, { options })

		fireEvent.click(document.getElementById("question-rev1") as HTMLElement)
		fireEvent.click(screen.getAllByRole("checkbox")[0])

		expect(onChange).toHaveBeenLastCalledWith(undefined)
	})

	it("draws a multi-select with nothing chosen when the answer is not a list", () => {
		renderField("multi_select", { answer: { kind: "value", value: "x" } }, { options })

		expect(document.getElementById("question-rev1-summary")?.textContent).toBe("report.multiSelect.placeholder")
	})

	it("draws a file question's attachments and a note that they are kept with the report", () => {
		renderField("file_upload", { attachments: [{ key: "k", name: "photo.png", size: 100, status: "uploaded", uploadId: "u" }] })

		expect(screen.getByText("report.attachments.keptWithReport")).toBeTruthy()
		expect(screen.getByText("photo.png")).toBeTruthy()
	})

	it("draws a phone question", () => {
		const { onChange } = renderField("phone")

		fireEvent.change(document.getElementById("question-rev1") as HTMLElement, { target: { value: "6045551234" } })

		expect(onChange).toHaveBeenCalledWith({ kind: "value", value: "(604) 555-1234", country: "CA" })
	})

	it("draws an email question, answering with the address or clearing it", () => {
		const { onChange } = renderField("email", { answer: { kind: "value", value: "x@y.ca" } }, { placeholderEn: "you@example.com" })

		expect((field() as HTMLInputElement).placeholder).toBe("you@example.com")
		fireEvent.change(field(), { target: { value: "a@b.ca" } })
		expect(onChange).toHaveBeenLastCalledWith({ kind: "value", value: "a@b.ca" })
		expect((field() as HTMLInputElement).value).toBe("x@y.ca")
		fireEvent.change(field(), { target: { value: "" } })
		expect(onChange).toHaveBeenLastCalledWith(undefined)
	})

	it("draws a date question, answering with the date or clearing it", () => {
		const { onChange } = renderField("date", { answer: { kind: "value", value: "2020-01-02" } }, { allowFutureDates: true })

		expect((field() as HTMLInputElement).value).toBe("2020-01-02")
		fireEvent.change(field(), { target: { value: "2020-01-03" } })
		expect(onChange).toHaveBeenLastCalledWith({ kind: "value", value: "2020-01-03" })
		fireEvent.change(field(), { target: { value: "" } })
		expect(onChange).toHaveBeenLastCalledWith(undefined)
	})

	it("draws short text, number, and time as inputs of their type", () => {
		const { onChange } = renderField("short_text", { answer: { kind: "value", value: "hi" } }, { placeholderFr: "x" })

		expect((field() as HTMLInputElement).type).toBe("text")
		expect((field() as HTMLInputElement).value).toBe("hi")
		fireEvent.change(field(), { target: { value: "hey" } })
		expect(onChange).toHaveBeenLastCalledWith({ kind: "value", value: "hey" })
		fireEvent.change(field(), { target: { value: "" } })
		expect(onChange).toHaveBeenLastCalledWith(undefined)
		cleanup()

		renderField("number")
		expect((field() as HTMLInputElement).type).toBe("number")
		cleanup()

		renderField("time")
		expect((field() as HTMLInputElement).type).toBe("time")
	})

	it("draws long text as a five-row textarea", () => {
		const { onChange } = renderField("long_text")

		expect(field().tagName).toBe("TEXTAREA")
		expect((field() as HTMLTextAreaElement).rows).toBe(5)
		fireEvent.change(field(), { target: { value: "long" } })
		expect(onChange).toHaveBeenLastCalledWith({ kind: "value", value: "long" })
	})

	it("reads the French wording in French", () => {
		renderField("short_text", { locale: "fr-CA" }, { labelFr: "Nom", helpTextFr: "Aide", placeholderFr: "Votre nom" })

		expect(screen.getByText("Nom", { exact: false }).textContent).toContain(":")
		expect(screen.getByText("Aide")).toBeTruthy()
		expect((field() as HTMLInputElement).placeholder).toBe("Votre nom")
	})
})
