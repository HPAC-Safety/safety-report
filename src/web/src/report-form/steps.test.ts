import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import type { PublicOptionView, PublicQuestionView } from "../api/publicQuestions"
import type { DraftAnswer } from "./draft"
import {
	answerProblem,
	answeredChoiceId,
	blockingQuestions,
	buildSteps,
	cannotBeAnswered,
	choiceScope,
	collectsNoAnswer,
	consistentAnswers,
	indexQuestionsById,
	isConditionMet,
	isMalformed,
	malformedAnswers,
	optionFor,
	optionGroups,
	optionLabel,
	optionTyped,
	questionHelp,
	questionLabel,
	questionPlaceholder,
	questionPrompt,
	scopedQuestion,
	unansweredRequired,
	visibleChildren,
	visibleSteps,
	type AnswerMap,
	type FormStep,
} from "./steps"

function option(id: string, labelEn: string, labelFr: string = labelEn, extra: Partial<PublicOptionView> = {}): PublicOptionView {
	return { id, code: id, labelEn, labelFr, onlyIn: null, pin: "none", aliases: [], ...extra }
}

function question(id: string, extra: Partial<PublicQuestionView> = {}): PublicQuestionView {
	return {
		id,
		key: id,
		role: "none",
		revisionId: `rev-${id}`,
		type: "text",
		isRequired: false,
		isPrivate: false,
		allowFutureDates: false,
		displayOrder: 0,
		dependsOnQuestionId: null,
		dependsOnChoiceId: null,
		allowsReporterAdditions: false,
		labelEn: `${id} en`,
		labelFr: `${id} fr`,
		helpTextEn: null,
		helpTextFr: null,
		placeholderEn: null,
		placeholderFr: null,
		options: [],
		children: [],
		...extra,
	}
}

function value(text: string, extra: Partial<Extract<DraftAnswer, { kind: "value" }>> = {}): DraftAnswer {
	return { kind: "value", value: text, ...extra }
}

function index(...questions: PublicQuestionView[]): Map<string, PublicQuestionView> {
	return indexQuestionsById(questions)
}

describe("steps", () => {
	describe("labels and text", () => {
		const q = question("q", { helpTextEn: "help en", helpTextFr: "help fr", placeholderEn: "ph en", placeholderFr: "ph fr" })

		it("picks the locale's wording", () => {
			expect(questionLabel(q, "en-CA")).toBe("q en")
			expect(questionLabel(q, "fr-CA")).toBe("q fr")
			expect(questionHelp(q, "en-CA")).toBe("help en")
			expect(questionHelp(q, "fr-CA")).toBe("help fr")
			expect(questionPlaceholder(q, "en-CA")).toBe("ph en")
			expect(questionPlaceholder(q, "fr-CA")).toBe("ph fr")
			expect(optionLabel(option("o", "Yes", "Oui"), "en-CA")).toBe("Yes")
			expect(optionLabel(option("o", "Yes", "Oui"), "fr-CA")).toBe("Oui")
		})

		it("adds the locale's closing colon to the prompt", () => {
			expect(questionPrompt(q, "en-CA")).toBe("q en:")
			expect(questionPrompt(q, "fr-CA")).toBe("q fr :")
			expect(questionPrompt(question("s", { type: "statement" }), "en-CA")).toBe("s en")
		})

		it("knows which questions collect no answer", () => {
			expect(collectsNoAnswer(question("a", { type: "statement" }))).toBe(true)
			expect(collectsNoAnswer(question("b", { type: "group" }))).toBe(true)
			expect(collectsNoAnswer(question("c"))).toBe(false)
		})
	})

	describe("choices", () => {
		const q = question("pick", {
			type: "single_select",
			options: [option("b", "Banana", "Banane"), option("a", "Apple", "Pomme", { pin: "first" }), option("c", "Cherry", "Cerise")],
		})

		it("groups options by pin in the locale's order", () => {
			const groups = optionGroups(q, "en-CA")
			expect(groups.map((group) => group.map((o) => o.id))).toEqual([["a"], ["b", "c"]])
		})

		it("finds an option by ID or by either label", () => {
			expect(optionFor(q, "b")?.id).toBe("b")
			expect(optionFor(q, "Banane")?.id).toBe("b")
			expect(optionFor(q, "Cherry")?.id).toBe("c")
			expect(optionFor(q, "missing")).toBeUndefined()
		})

		it("finds a typed option ignoring case and spacing", () => {
			expect(optionTyped(q, "  apple ")?.id).toBe("a")
			expect(optionTyped(q, "CERISE")?.id).toBe("c")
			expect(optionTyped(q, "kiwi")).toBeUndefined()
		})

		describe("answeredChoiceId", () => {
			const ahead = question("ahead", { type: "autocomplete", options: q.options })

			it("is undefined without a usable value answer", () => {
				expect(answeredChoiceId(q, undefined)).toBeUndefined()
				expect(answeredChoiceId(q, { kind: "options", values: ["a"] })).toBeUndefined()
				expect(answeredChoiceId(q, value("  "))).toBeUndefined()
			})

			it("resolves a picker answer", () => {
				expect(answeredChoiceId(q, value("b"))).toBe("b")
			})

			it("uses a type-ahead's picked choice when it is offered", () => {
				expect(answeredChoiceId(ahead, value("whatever", { choice: "c" }))).toBe("c")
			})

			it("falls back to the typed words when the picked choice is not offered or absent", () => {
				expect(answeredChoiceId(ahead, value("Apple", { choice: "gone" }))).toBe("a")
				expect(answeredChoiceId(ahead, value("Banana"))).toBe("b")
				expect(answeredChoiceId(ahead, value("Kiwi"))).toBeUndefined()
			})
		})
	})

	describe("dependent choices", () => {
		const country = question("country", {
			type: "single_select",
			options: [option("ca", "Canada"), option("us", "USA")],
		})
		const city = question("city", {
			type: "single_select",
			choicesDependOnQuestionId: "country",
			options: [
				option("van", "Vancouver", "Vancouver", { parentChoiceIds: ["ca"] }),
				option("nyc", "New York", "New York", { parentChoiceIds: ["us"] }),
				option("any", "Anywhere"),
			],
		})
		const byId = index(country, city)

		it("offers everything when no parent is named or the parent is not on the form", () => {
			expect(choiceScope(country, {}, byId)).toEqual({ kind: "all" })
			const orphan = question("o", { choicesDependOnQuestionId: "ghost" })
			expect(choiceScope(orphan, {}, byId)).toEqual({ kind: "all" })
			expect(scopedQuestion(city, {}, new Map())).toBe(city)
		})

		it("waits while the parent is unanswered", () => {
			expect(choiceScope(city, {}, byId).kind).toBe("waiting")
			expect(choiceScope(city, { [country.revisionId]: { kind: "options", values: [] } }, byId).kind).toBe("waiting")
			expect(choiceScope(city, { [country.revisionId]: value(" ") }, byId).kind).toBe("waiting")
			expect(scopedQuestion(city, {}, byId).options).toEqual([])
			expect(cannotBeAnswered(city, {}, byId)).toBe(true)
		})

		it("offers only the choices under the parent's answer", () => {
			const answers: AnswerMap = { [country.revisionId]: value("ca") }
			const scope = choiceScope(city, answers, byId)
			expect(scope.kind).toBe("under")
			expect(scopedQuestion(city, answers, byId).options.map((o) => o.id)).toEqual(["van"])
			expect(cannotBeAnswered(city, answers, byId)).toBe(false)
		})

		it("offers nothing when the parent's answer names no choice", () => {
			const answers: AnswerMap = { [country.revisionId]: value("Mars") }
			expect(scopedQuestion(city, answers, byId).options).toEqual([])
			expect(cannotBeAnswered(city, answers, byId)).toBe(true)
		})

		it("never blocks a type-ahead whose parent is answered", () => {
			const ahead = question("ahead", { type: "autocomplete", choicesDependOnQuestionId: "country", options: [] })
			const map = index(country, ahead)
			expect(cannotBeAnswered(ahead, { [country.revisionId]: value("ca") }, map)).toBe(false)
			expect(cannotBeAnswered(question("plain"), {}, map)).toBe(false)
		})
	})

	describe("consistentAnswers", () => {
		const country = question("country", { type: "single_select", options: [option("ca", "Canada"), option("us", "USA")] })
		const city = question("city", {
			type: "single_select",
			choicesDependOnQuestionId: "country",
			options: [option("van", "Vancouver", "Vancouver", { parentChoiceIds: ["ca"] }), option("nyc", "New York", "New York", { parentChoiceIds: ["us"] })],
		})
		const town = question("town", {
			type: "autocomplete",
			choicesDependOnQuestionId: "country",
			options: [option("t1", "Burnaby", "Burnaby", { parentChoiceIds: ["ca"] })],
		})
		const byId = index(country, city, town)

		it("returns the same object when nothing needs removing", () => {
			const answers: AnswerMap = {
				[country.revisionId]: value("ca"),
				[city.revisionId]: value("van"),
				[town.revisionId]: value("t1", { choice: "t1" }),
			}
			expect(consistentAnswers(answers, byId)).toBe(answers)
		})

		it("skips option answers and questions with no dependency", () => {
			const answers: AnswerMap = { [country.revisionId]: { kind: "options", values: ["x"] }, [city.revisionId]: { kind: "options", values: [] } }
			expect(consistentAnswers(answers, byId)).toBe(answers)
		})

		it("removes a picker answer its parent no longer allows", () => {
			const answers: AnswerMap = { [country.revisionId]: value("us"), [city.revisionId]: value("van") }
			const result = consistentAnswers(answers, byId)
			expect(result[city.revisionId]).toBeUndefined()
			expect(result[country.revisionId]).toBeDefined()
			expect(answers[city.revisionId]).toBeDefined()
		})

		it("removes answers while the parent is unanswered", () => {
			const result = consistentAnswers({ [city.revisionId]: value("van") }, byId)
			expect(result[city.revisionId]).toBeUndefined()
		})

		it("removes a type-ahead's picked choice that is not offered but keeps typed words", () => {
			const stale = consistentAnswers({ [country.revisionId]: value("us"), [town.revisionId]: value("Burnaby", { choice: "t1" }) }, byId)
			expect(stale[town.revisionId]).toBeUndefined()
			const typed = { [country.revisionId]: value("us"), [town.revisionId]: value("Something new") }
			expect(consistentAnswers(typed, byId)).toBe(typed)
		})
	})

	describe("indexQuestionsById and buildSteps", () => {
		const child = question("child")
		const group = question("grp", { type: "group", children: [child] })
		const intro = question("intro", { type: "statement" })
		const later = question("later", { type: "statement" })
		const plain = question("plain")

		it("indexes questions and their children", () => {
			const map = indexQuestionsById([group, plain])
			expect([...map.keys()].sort()).toEqual(["child", "grp", "plain"])
		})

		it("makes a leading statement the intro, groups pages and the rest questions", () => {
			const steps = buildSteps([intro, group, later, plain])
			expect(steps.map((step) => step.kind)).toEqual(["intro", "group", "question", "question"])
		})
	})

	describe("conditions", () => {
		const yesNo = question("yn", { type: "yes_no" })
		const dependent = question("dep", { dependsOnQuestionId: "yn" })
		const picker = question("pk", { type: "single_select", options: [option("x", "Ex", "Ex-fr"), option("y", "Why")] })
		const onX = question("onx", { dependsOnQuestionId: "pk", dependsOnChoiceId: "x" })
		const publish = question("pub", { role: "consent_publish", type: "yes_no" })
		const media = question("media", { role: "consent_media", type: "yes_no" })
		const byId = index(yesNo, dependent, picker, onX, publish, media)

		it("is always met without a dependency", () => {
			expect(isConditionMet(yesNo, {}, byId)).toBe(true)
		})

		it("is met when the parent cannot be found", () => {
			expect(isConditionMet(question("z", { dependsOnQuestionId: "ghost" }), {}, byId)).toBe(true)
		})

		it("follows a yes/no parent", () => {
			expect(isConditionMet(dependent, {}, byId)).toBe(false)
			expect(isConditionMet(dependent, { [yesNo.revisionId]: { kind: "options", values: ["yes"] } }, byId)).toBe(false)
			expect(isConditionMet(dependent, { [yesNo.revisionId]: value("no") }, byId)).toBe(false)
			expect(isConditionMet(dependent, { [yesNo.revisionId]: value("yes") }, byId)).toBe(true)
		})

		it("follows a single-select parent's choice by ID or label", () => {
			expect(isConditionMet(onX, { [picker.revisionId]: value("x") }, byId)).toBe(true)
			expect(isConditionMet(onX, { [picker.revisionId]: value("Ex-fr") }, byId)).toBe(true)
			expect(isConditionMet(onX, { [picker.revisionId]: value("y") }, byId)).toBe(false)
			expect(isConditionMet(onX, { [picker.revisionId]: value("nonsense") }, byId)).toBe(false)
		})

		it("asks media consent only with an attachment and publication consent yes", () => {
			const yes: AnswerMap = { [publish.revisionId]: value("yes") }
			expect(isConditionMet(media, yes, byId, false)).toBe(false)
			expect(isConditionMet(media, yes, byId, true)).toBe(true)
			expect(isConditionMet(media, { [publish.revisionId]: value("no") }, byId, true)).toBe(false)
			expect(isConditionMet(media, { [publish.revisionId]: { kind: "options", values: ["yes"] } }, byId, true)).toBe(false)
			expect(isConditionMet(media, {}, byId, true)).toBe(false)
			expect(isConditionMet(media, yes, index(media), true)).toBe(false)
		})
	})

	describe("visible steps and children", () => {
		const yesNo = question("yn", { type: "yes_no" })
		const hidden = question("hid", { dependsOnQuestionId: "yn" })
		const shown = question("shown")
		const group = question("grp", { type: "group", children: [hidden] })
		const mixedGroup = question("mixed", { type: "group", children: [hidden, shown] })
		const intro = question("intro", { type: "statement" })
		const byId = index(yesNo, group, mixedGroup, intro)
		const steps: FormStep[] = buildSteps([intro, yesNo, hidden, group, mixedGroup])

		it("hides conditional questions and empty groups", () => {
			expect(visibleSteps(steps, {}, byId).map((step) => step.question.id)).toEqual(["intro", "yn", "mixed"])
		})

		it("shows them once the condition holds", () => {
			const answers: AnswerMap = { [yesNo.revisionId]: value("yes") }
			expect(visibleSteps(steps, answers, byId).map((step) => step.question.id)).toEqual(["intro", "yn", "hid", "grp", "mixed"])
		})

		it("lists a group's visible children", () => {
			expect(visibleChildren(mixedGroup, {}, byId).map((c) => c.id)).toEqual(["shown"])
		})
	})

	describe("required questions", () => {
		const required = question("req", { isRequired: true })
		const optional = question("opt")
		const statement = question("st", { type: "statement", isRequired: true })
		const waiting = question("wait", {
			type: "single_select",
			isRequired: true,
			choicesDependOnQuestionId: "req",
			options: [option("o", "O", "O", { parentChoiceIds: ["z"] })],
		})
		const group = question("grp", { type: "group", children: [required, optional] })
		const byId = index(required, optional, statement, waiting, group)

		it("never blocks an intro", () => {
			expect(unansweredRequired({ kind: "intro", question: statement }, {}, byId)).toEqual([])
		})

		it("reports an unanswered required question", () => {
			expect(unansweredRequired({ kind: "question", question: required }, {}, byId)).toEqual([required])
		})

		it("accepts value and option answers", () => {
			expect(unansweredRequired({ kind: "question", question: required }, { [required.revisionId]: value("x") }, byId)).toEqual([])
			expect(unansweredRequired({ kind: "question", question: required }, { [required.revisionId]: { kind: "options", values: ["a"] } }, byId)).toEqual([])
		})

		it("treats a blank value or empty option list as unanswered", () => {
			expect(unansweredRequired({ kind: "question", question: required }, { [required.revisionId]: value("  ") }, byId)).toEqual([required])
			expect(unansweredRequired({ kind: "question", question: required }, { [required.revisionId]: { kind: "options", values: [] } }, byId)).toEqual([required])
		})

		it("ignores optional questions, statements, and questions that cannot be answered yet", () => {
			expect(unansweredRequired({ kind: "question", question: optional }, {}, byId)).toEqual([])
			expect(unansweredRequired({ kind: "question", question: statement }, {}, byId)).toEqual([])
			expect(unansweredRequired({ kind: "question", question: waiting }, {}, byId)).toEqual([])
		})

		it("reports a group's visible unanswered required children", () => {
			expect(unansweredRequired({ kind: "group", question: group }, {}, byId)).toEqual([required])
		})
	})

	describe("malformed answers", () => {
		beforeEach(() => {
			vi.useFakeTimers()
			vi.setSystemTime(new Date("2026-03-01T12:00:00"))
		})

		afterEach(() => {
			vi.useRealTimers()
		})

		const email = question("email", { type: "email" })
		const phone = question("phone", { type: "phone" })
		const date = question("date", { type: "date" })
		const futureDate = question("fdate", { type: "date", allowFutureDates: true })
		const text = question("text")

		it("never faults a missing, non-value, or blank answer", () => {
			expect(answerProblem(email, undefined)).toBeNull()
			expect(answerProblem(email, { kind: "options", values: ["x"] })).toBeNull()
			expect(answerProblem(email, value("   "))).toBeNull()
			expect(isMalformed(email, undefined)).toBe(false)
		})

		it("checks emails", () => {
			expect(answerProblem(email, value(" a@b.ca "))).toBeNull()
			expect(answerProblem(email, value("nope"))).toBe("report.email.invalid")
			expect(isMalformed(email, value("nope"))).toBe(true)
		})

		it("checks phones against the answer's country, defaulting to Canada", () => {
			expect(answerProblem(phone, value("6045551234"))).toBeNull()
			expect(answerProblem(phone, value("6045551234", { country: "CA" }))).toBeNull()
			expect(answerProblem(phone, value("12"))).toBe("report.phone.invalid")
		})

		it("checks dates", () => {
			expect(answerProblem(date, value("2026-02-30"))).toBe("report.date.invalid")
			expect(answerProblem(date, value("2026-02-28"))).toBeNull()
			expect(answerProblem(date, value("2026-03-02"))).toBe("report.date.future")
			expect(answerProblem(futureDate, value("2026-03-02"))).toBeNull()
		})

		it("does not check other types", () => {
			expect(answerProblem(text, value("anything"))).toBeNull()
		})

		it("lists malformed answers on a step", () => {
			const group = question("grp", { type: "group", children: [email, text] })
			const byId = index(email, text, group)
			const answers: AnswerMap = { [email.revisionId]: value("nope") }
			expect(malformedAnswers({ kind: "intro", question: text }, answers, byId)).toEqual([])
			expect(malformedAnswers({ kind: "question", question: email }, answers, byId)).toEqual([email])
			expect(malformedAnswers({ kind: "group", question: group }, answers, byId)).toEqual([email])
		})

		it("combines unanswered and malformed questions as blocking", () => {
			const required = question("req", { type: "email", isRequired: true })
			const byId = index(required)
			expect(blockingQuestions({ kind: "question", question: required }, {}, byId)).toEqual([required])
			expect(blockingQuestions({ kind: "question", question: required }, { [required.revisionId]: value("bad") }, byId)).toEqual([required])
			expect(blockingQuestions({ kind: "question", question: required }, { [required.revisionId]: value("a@b.ca") }, byId)).toEqual([])
		})
	})
})
