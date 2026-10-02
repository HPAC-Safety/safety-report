import { useLocale } from "../i18n/useLocale"
import { sortChoices } from "../lib/sortChoices"
import type { TypeAheadValueView } from "../api/adminQuestions"
import { TypeAheadParentLinkView } from "./TypeAheadParentLink.view"

export interface TypeAheadParentLinkProps {
	valueId: string
	parent: NonNullable<TypeAheadValueView["parent"]>
	chosen: string[]
	onChoose: (choiceIds: string[]) => void
	onRelink: (choiceIds: string[]) => void
	wording: (choice: { labelEn: string | null; labelFr: string | null }) => string
}

/**
 * The view model of the parent choices a dependent type-ahead's value is
 * offered under: the parent question's label in the reader's language, the
 * choices sorted, the ones it is offered under now, and whether the pending
 * selection changes them. A value is offered under at least one, so the last
 * one ticked cannot be unticked, unless the value has none yet (ADR-0151).
 */
export function useTypeAheadParentLink({ parent, chosen, wording }: TypeAheadParentLinkProps) {
	const { locale } = useLocale()
	const question = locale === "fr-CA" ? parent.questionLabelFr : parent.questionLabelEn
	const sorted = sortChoices(parent.choices, locale, wording)
	const current = sorted.filter((choice) => parent.parentChoiceIds.includes(choice.id))
	const unchanged = chosen.length === current.length && current.every((choice) => chosen.includes(choice.id))

	return { question, sorted, current, unchanged }
}

export type TypeAheadParentLinkModel = ReturnType<typeof useTypeAheadParentLink>

export function TypeAheadParentLink(props: TypeAheadParentLinkProps) {
	return <TypeAheadParentLinkView {...props} {...useTypeAheadParentLink(props)} />
}
