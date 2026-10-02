import type { FormStep } from "./steps"

/** The introduction is the bare form address; every other page is named by its heading question's key (ADR-0099). */
export function stepPath(step: FormStep): string {
	return step.kind === "intro" ? "/report" : `/report/${encodeURIComponent(step.question.key)}`
}
