import { useCallback, useEffect, useMemo, useState } from "react";
import { useLocale } from "../i18n/useLocale";
import { sortChoices } from "../lib/sortChoices";
import { MultiSelectPicker } from "../report-form/MultiSelectPicker";
import {
	ApiError,
	approveTypeAheadValue,
	correctTypeAheadValue,
	listTypeAheadValuesAwaitingReview,
	mergeTypeAheadValue,
	setTypeAheadValueParents,
	removeTypeAheadValue,
	type TypeAheadValueView,
} from "../api/adminQuestions";

/*
 * The type-ahead values waiting for a Safety Officer or Administrator
 * (ADR-0129). A reporter's new value is offered to the next reporter at once;
 * this is where someone who reads the reports looks at it afterwards.
 *
 * Each value can be approved as it stands, corrected in place — the same
 * value, so every answer that names it reads the correction — merged into
 * another value of its question, which every answer naming it then reads, or
 * removed: no longer offered, while every answer that named it still does. A removed
 * value a reporter typed again comes back here, still removed.
 *
 * The queue groups values by question — one heading per question, A→Z — and
 * sorts values A→Z within each group, both collated in the viewer's language
 * (issue #651). Every action refetches the queue in place, without ever
 * re-entering the loading state, so the list stays mounted and the
 * reviewer's scroll position and place in a run of duplicates survive.
 */

type Draft = { labelEn: string; labelFr: string };

// A call signature rather than an arrow type: tools/check-hardcoded-strings.mjs
// is a line scanner and reads an arrow's `>` as the end of a tag.
type ReviewAction = { (): Promise<void> };

type QuestionGroup = {
	questionId: string;
	heading: string;
	values: TypeAheadValueView[];
};

/** The queue's values, grouped by question and sorted A→Z within each group, then the groups sorted A→Z by heading — both in `locale`'s collation (ADR-0136's shared helper). */
function groupByQuestion(
	values: readonly TypeAheadValueView[],
	locale: string,
	wording: (value: {
		labelEn: string | null;
		labelFr: string | null;
	}) => string,
): QuestionGroup[] {
	const byQuestion = new Map<string, TypeAheadValueView[]>();
	for (const value of values) {
		const group = byQuestion.get(value.questionId);
		if (group) group.push(value);
		else byQuestion.set(value.questionId, [value]);
	}

	const collator = new Intl.Collator(locale, {
		sensitivity: "base",
		numeric: true,
	});
	const groups = [...byQuestion.entries()].map(([questionId, groupValues]) => ({
		questionId,
		heading:
			locale === "fr-CA"
				? groupValues[0].questionLabelFr
				: groupValues[0].questionLabelEn,
		values: sortChoices(groupValues, locale, wording),
	}));
	groups.sort(
		(left, right) =>
			collator.compare(left.heading, right.heading) ||
			(left.questionId < right.questionId
				? -1
				: left.questionId > right.questionId
					? 1
					: 0),
	);
	return groups;
}

export function ReviewTypeAheadValuesPage() {
	const { t, locale } = useLocale();
	const [values, setValues] = useState<TypeAheadValueView[]>([]);
	const [drafts, setDrafts] = useState<Record<string, Draft>>({});
	const [mergeInto, setMergeInto] = useState<Record<string, string>>({});
	const [relinkTo, setRelinkTo] = useState<Record<string, string[]>>({});
	const [error, setError] = useState<string | null>(null);
	const [loading, setLoading] = useState(true);

	const report = useCallback(
		(cause: unknown) =>
			setError(
				cause instanceof ApiError
					? cause.detail
					: t("typeAheadValues.error.unexpected"),
			),
		[t],
	);

	// Refetches the queue without touching `loading`, so the list stays mounted
	// and the reviewer's scroll position is unchanged (issue #651).
	const refresh = useCallback(async () => {
		const queue = await listTypeAheadValuesAwaitingReview();
		setValues(queue.values);
		setError(null);
	}, []);

	const load = useCallback(async () => {
		try {
			setLoading(true);
			await refresh();
		} catch (cause) {
			report(cause);
		} finally {
			setLoading(false);
		}
	}, [refresh, report]);

	useEffect(() => {
		void load();
	}, [load]);

	async function act(action: ReviewAction, id: string) {
		try {
			await action();
			setDrafts((current) => {
				const { [id]: _done, ...rest } = current;
				return rest;
			});
			await refresh();
		} catch (cause) {
			report(cause);
		}
	}

	function startCorrecting(value: TypeAheadValueView) {
		setDrafts((current) => ({
			...current,
			[value.id]: {
				labelEn: value.labelEn ?? "",
				labelFr: value.labelFr ?? "",
			},
		}));
	}

	function wording(value: { labelEn: string | null; labelFr: string | null }) {
		return (
			(locale === "fr-CA"
				? (value.labelFr ?? value.labelEn)
				: (value.labelEn ?? value.labelFr)) ?? ""
		);
	}

	const groups = useMemo(
		() => groupByQuestion(values, locale, wording),
		[values, locale],
	);

	return (
		<main className="mx-auto max-w-4xl px-6 py-12">
			<h1 className="font-display text-3xl font-bold">
				{t("typeAheadValues.title")}
			</h1>
			<p className="mt-2 font-sans text-ink-muted">
				{t("typeAheadValues.intro")}
			</p>

			{error && (
				<p
					role="alert"
					className="mt-6 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink"
				>
					{error}
				</p>
			)}

			{loading ? (
				<p className="mt-8 font-sans text-ink-muted">
					{t("typeAheadValues.loading")}
				</p>
			) : values.length === 0 ? (
				<p className="mt-8 font-sans text-ink-muted">
					{t("typeAheadValues.empty")}
				</p>
			) : (
				<div className="mt-8 flex flex-col gap-8">
					{groups.map((group) => (
						<section
							key={group.questionId}
							aria-labelledby={`type-ahead-group-${group.questionId}`}
						>
							<h2
								id={`type-ahead-group-${group.questionId}`}
								className="font-display text-xl font-semibold text-ink"
							>
								{group.heading}
							</h2>
							<ul className="mt-4 flex flex-col gap-4">
								{group.values.map((value) => {
									const draft = drafts[value.id];
									return (
										<li
											key={value.id}
											data-testid="type-ahead-value"
											className="rounded border border-rule bg-surface p-4"
										>
											<p
												data-testid="type-ahead-value-wording"
												className="font-sans text-lg font-medium text-ink"
											>
												{wording(value)}
											</p>
											<p className="font-sans text-sm text-ink-muted">
												{value.typedIn
													? t("typeAheadValues.typedIn", {
															locale: value.typedIn,
														})
													: t("typeAheadValues.written")}
												{" · "}
												{t("typeAheadValues.answerCount", {
													count: String(value.answerCount),
												})}
												{value.isRemoved &&
													` · ${t("typeAheadValues.removedBadge")}`}
											</p>

											{value.parent && (
												<ParentLink
													valueId={value.id}
													parent={value.parent}
													// Only live parent choices are shown and counted; a link to one since
													// removed is kept by the server, and the page never sends it.
													chosen={
														relinkTo[value.id] ??
														value.parent.parentChoiceIds.filter((id) =>
															value.parent!.choices.some(
																(choice) => choice.id === id,
															),
														)
													}
													onChoose={(choiceIds) =>
														setRelinkTo((current) => ({
															...current,
															[value.id]: choiceIds,
														}))
													}
													onRelink={(choiceIds) =>
														void act(
															() =>
																setTypeAheadValueParents(value.id, choiceIds),
															value.id,
														)
													}
													wording={wording}
												/>
											)}

											{draft ? (
												<div className="mt-4 grid gap-3 sm:grid-cols-2">
													<label className="block font-sans text-sm text-ink">
														{t("typeAheadValues.labelEn")}
														<input
															type="text"
															className="mt-1 w-full rounded border border-rule bg-surface-2 px-3 py-2 font-sans text-ink"
															value={draft.labelEn}
															onChange={(event) =>
																setDrafts((current) => ({
																	...current,
																	[value.id]: {
																		...draft,
																		labelEn: event.target.value,
																	},
																}))
															}
														/>
													</label>
													<label className="block font-sans text-sm text-ink">
														{t("typeAheadValues.labelFr")}
														<input
															type="text"
															className="mt-1 w-full rounded border border-rule bg-surface-2 px-3 py-2 font-sans text-ink"
															value={draft.labelFr}
															onChange={(event) =>
																setDrafts((current) => ({
																	...current,
																	[value.id]: {
																		...draft,
																		labelFr: event.target.value,
																	},
																}))
															}
														/>
													</label>
												</div>
											) : null}

											<div className="mt-3 flex flex-wrap gap-3">
												{draft ? (
													<>
														<button
															type="button"
															disabled={
																!draft.labelEn.trim() && !draft.labelFr.trim()
															}
															className="touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600 disabled:opacity-50"
															onClick={() =>
																void act(
																	() =>
																		correctTypeAheadValue(
																			value.id,
																			draft.labelEn,
																			draft.labelFr,
																		),
																	value.id,
																)
															}
														>
															{t("typeAheadValues.saveCorrection")}
														</button>
														<button
															type="button"
															className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink"
															onClick={() =>
																setDrafts((current) => {
																	const { [value.id]: _cancelled, ...rest } =
																		current;
																	return rest;
																})
															}
														>
															{t("typeAheadValues.cancel")}
														</button>
													</>
												) : (
													<>
														<button
															type="button"
															className="touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600"
															onClick={() =>
																void act(
																	() => approveTypeAheadValue(value.id),
																	value.id,
																)
															}
														>
															{t("typeAheadValues.approve")}
														</button>
														<button
															type="button"
															className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink"
															onClick={() => startCorrecting(value)}
														>
															{t("typeAheadValues.correct")}
														</button>
														{!value.isRemoved && (
															<button
																type="button"
																className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink"
																onClick={() =>
																	void act(
																		() => removeTypeAheadValue(value.id),
																		value.id,
																	)
																}
															>
																{t("typeAheadValues.remove")}
															</button>
														)}
														{value.mergeTargets.length > 0 && (
															<span className="flex flex-wrap items-center gap-2">
																<label className="font-sans text-sm text-ink">
																	<span className="sr-only">
																		{t("typeAheadValues.mergeInto")}
																	</span>
																	<select
																		aria-label={t("typeAheadValues.mergeInto")}
																		className="touch-target rounded border border-rule bg-surface-2 px-3 font-sans text-ink"
																		value={mergeInto[value.id] ?? ""}
																		onChange={(event) =>
																			setMergeInto((current) => ({
																				...current,
																				[value.id]: event.target.value,
																			}))
																		}
																	>
																		<option value="">
																			{t("typeAheadValues.mergeInto")}
																		</option>
																		{sortChoices(
																			value.mergeTargets,
																			locale,
																			wording,
																		).map((target) => (
																			<option key={target.id} value={target.id}>
																				{wording(target)}
																			</option>
																		))}
																	</select>
																</label>
																<button
																	type="button"
																	disabled={!mergeInto[value.id]}
																	className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink disabled:opacity-50"
																	onClick={() =>
																		void act(
																			() =>
																				mergeTypeAheadValue(
																					value.id,
																					mergeInto[value.id]!,
																				),
																			value.id,
																		)
																	}
																>
																	{t("typeAheadValues.merge")}
																</button>
															</span>
														)}
													</>
												)}
											</div>
										</li>
									);
								})}
							</ul>
						</section>
					))}
				</div>
			)}
		</main>
	);
}

/**
 * The parent choices a dependent type-ahead's value is offered under, and a
 * multi-select to add or remove them. A value is offered under at least one, so
 * the last one ticked cannot be unticked, unless the value has none yet
 * (ADR-0151).
 */
function ParentLink({
	valueId,
	parent,
	chosen,
	onChoose,
	onRelink,
	wording,
}: {
	valueId: string;
	parent: NonNullable<TypeAheadValueView["parent"]>;
	chosen: string[];
	onChoose: (choiceIds: string[]) => void;
	onRelink: (choiceIds: string[]) => void;
	wording: (choice: {
		labelEn: string | null;
		labelFr: string | null;
	}) => string;
}) {
	const { t, locale } = useLocale();
	const question =
		locale === "fr-CA" ? parent.questionLabelFr : parent.questionLabelEn;
	const sorted = sortChoices(parent.choices, locale, wording);
	const current = sorted.filter((choice) =>
		parent.parentChoiceIds.includes(choice.id),
	);
	const unchanged =
		chosen.length === current.length &&
		current.every((choice) => chosen.includes(choice.id));

	return (
		<div className="mt-3 flex flex-wrap items-end gap-2">
			<p
				data-testid="type-ahead-value-parent"
				className="w-full font-sans text-sm text-ink"
			>
				{current.length > 0
					? t("typeAheadValues.offeredUnder", {
							question,
							choice: current.map(wording).join(", "),
						})
					: t("typeAheadValues.offeredUnderNothing", { question })}
			</p>
			<div className="min-w-64" data-testid="type-ahead-value-parent-choice">
				<MultiSelectPicker
					fieldId={`type-ahead-value-parent-${valueId}`}
					label={t("typeAheadValues.relinkTo", { question })}
					groups={[
						sorted.map((choice) => ({
							key: choice.id,
							label: wording(choice),
						})),
					]}
					values={chosen}
					placeholder={t("typeAheadValues.relinkNone")}
					describedBy={undefined}
					locked={chosen.length === 1 ? chosen : []}
					lockedReason={t("typeAheadValues.lastParent")}
					onToggle={(id) =>
						onChoose(
							chosen.includes(id)
								? chosen.filter((ticked) => ticked !== id)
								: [...chosen, id],
						)
					}
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
	);
}
