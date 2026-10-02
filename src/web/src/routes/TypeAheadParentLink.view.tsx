import { useLocale } from "../i18n/useLocale"
import { MultiSelectPicker } from "../report-form/MultiSelectPicker"
import type { TypeAheadParentLinkModel, TypeAheadParentLinkProps } from "./TypeAheadParentLink"

export type TypeAheadParentLinkViewProps = TypeAheadParentLinkProps & TypeAheadParentLinkModel

/** Where a value is offered now, and a multi-select to add or remove the parent choices it is offered under. */
export function TypeAheadParentLinkView({
	valueId,
	chosen,
	onChoose,
	onRelink,
	wording,
	question,
	sorted,
	current,
	unchanged,
}: TypeAheadParentLinkViewProps) {
	const { t } = useLocale()

	return (
		<div className="mt-3 flex flex-wrap items-end gap-2">
			<p data-testid="type-ahead-value-parent" className="w-full font-sans text-sm text-ink">
				{current.length > 0
					? t("typeAheadValues.offeredUnder", { question, choice: current.map(wording).join(", ") })
					: t("typeAheadValues.offeredUnderNothing", { question })}
			</p>
			<div className="min-w-64" data-testid="type-ahead-value-parent-choice">
				<MultiSelectPicker
					fieldId={`type-ahead-value-parent-${valueId}`}
					label={t("typeAheadValues.relinkTo", { question })}
					groups={[sorted.map((choice) => ({ key: choice.id, label: wording(choice) }))]}
					values={chosen}
					placeholder={t("typeAheadValues.relinkNone")}
					describedBy={undefined}
					locked={chosen.length === 1 ? chosen : []}
					lockedReason={t("typeAheadValues.lastParent")}
					onToggle={(id) => onChoose(chosen.includes(id) ? chosen.filter((ticked) => ticked !== id) : [...chosen, id])}
				/>
			</div>
			<button
				type="button"
				disabled={chosen.length === 0 || unchanged}
				className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink disabled:opacity-50"
				onClick={() => onRelink(chosen)}
			>
				{t("typeAheadValues.relink")}
			</button>
		</div>
	)
}
