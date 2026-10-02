import { describe, expect, it } from "vitest"

import type { PublicQuestionView } from "../api/publicQuestions"
import { stepPath } from "./stepPath"

const question = { key: "what happened/where" } as PublicQuestionView

describe("stepPath", () => {
	it("addresses the introduction as the bare form", () => {
		expect(stepPath({ kind: "intro", question })).toBe("/report")
	})

	it("addresses every other page by its encoded heading key", () => {
		expect(stepPath({ kind: "question", question })).toBe("/report/what%20happened%2Fwhere")
		expect(stepPath({ kind: "group", question })).toBe("/report/what%20happened%2Fwhere")
	})
})
