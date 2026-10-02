import { useLocale } from "../i18n/useLocale"
import { TranslationDirectionSwitch } from "../components/TranslationDirectionSwitch"
import type { ReviewTypeAheadValuesPageModel } from "./ReviewTypeAheadValuesPage"
import { TypeAheadParentLink } from "./TypeAheadParentLink"

export type ReviewTypeAheadValuesPageViewProps = ReviewTypeAheadValuesPageModel

/** The review queue: values grouped by question, each with its actions, correction form and translate controls. */
export function ReviewTypeAheadValuesPageView({ error, loading, isEmpty, canTranslate, wording, groups }: ReviewTypeAheadValuesPageViewProps) {
	const { t } = useLocale()

	return (
		<main className="mx-auto max-w-4xl px-6 py-12">
			<h1 className="font-display text-3xl font-bold">{t("typeAheadValues.title")}</h1>
			<p className="mt-2 font-sans text-ink-muted">{t("typeAheadValues.intro")}</p>

			{error && (
				<p role="alert" className="mt-6 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{error}
				</p>
			)}

			{loading ? (
				<p className="mt-8 font-sans text-ink-muted">{t("typeAheadValues.loading")}</p>
			) : isEmpty ? (
				<p className="mt-8 font-sans text-ink-muted">{t("typeAheadValues.empty")}</p>
			) : (
				<div className="mt-8 flex flex-col gap-8">
					{groups.map((group) => (
						<section key={group.id} aria-labelledby={`type-ahead-group-${group.id}`}>
							<h2 id={`type-ahead-group-${group.id}`} className="font-display text-xl font-semibold text-ink">
								{group.heading}
							</h2>
							<ul className="mt-4 flex flex-col gap-4">
								{group.cards.map((card) => {
									const { value, draft } = card
									return (
										<li key={value.id} data-testid="type-ahead-value" className="rounded border border-rule bg-surface p-4">
											<p data-testid="type-ahead-value-wording" className="font-sans text-lg font-medium text-ink">
												{card.text}
											</p>
											<p className="font-sans text-sm text-ink-muted">
												{value.typedIn
													? t("typeAheadValues.typedIn", { locale: value.typedIn })
													: t("typeAheadValues.written")}
												{" · "}
												{t("typeAheadValues.answerCount", { count: String(value.answerCount) })}
												{value.isRemoved && ` · ${t("typeAheadValues.removedBadge")}`}
											</p>

											{card.hasAliases && (
												<p
													data-testid="type-ahead-value-aliases"
													className="font-sans text-sm text-ink-muted"
												>
													{t("typeAheadValues.aliases", {
														aliases: card.aliasesText,
													})}
												</p>
											)}

											{value.parent && (
												<TypeAheadParentLink
													valueId={value.id}
													parent={value.parent}
													chosen={card.parentChosen}
													onChoose={card.onChooseParent}
													onRelink={card.onRelink}
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
															onChange={(event) => card.onLabelEn(event.target.value)}
														/>
													</label>
													<label className="block font-sans text-sm text-ink">
														{t("typeAheadValues.labelFr")}
														<input
															type="text"
															className="mt-1 w-full rounded border border-rule bg-surface-2 px-3 py-2 font-sans text-ink"
															value={draft.labelFr}
															onChange={(event) => card.onLabelFr(event.target.value)}
														/>
													</label>
												</div>
											) : null}

											{draft && (
												<div
													role="group"
													aria-label={t("typeAheadValues.translate.group")}
													className="mt-3 flex flex-wrap items-center gap-3"
												>
													<TranslationDirectionSwitch direction={draft.direction} onChange={card.onDirection} />
													<button
														type="button"
														className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface-2 disabled:opacity-40"
														disabled={card.translateDisabled}
														onClick={card.onTranslate}
													>
														{draft.translating ? t("typeAheadValues.translate.working") : t("typeAheadValues.translate.action")}
													</button>
													<p className="font-sans text-xs text-ink-muted">
														{!canTranslate ? t("typeAheadValues.translate.unavailable") : t("typeAheadValues.translate.hint")}
													</p>
												</div>
											)}

											{draft?.translationError && (
												<p role="alert" className="mt-2 font-sans text-sm text-ink">
													{draft.translationError}
												</p>
											)}

											<div className="mt-3 flex flex-wrap gap-3">
												{draft ? (
													<>
														<button
															type="button"
															disabled={!draft.labelEn.trim() && !draft.labelFr.trim()}
															className="touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600 disabled:opacity-50"
															onClick={card.onSaveCorrection}
														>
															{t("typeAheadValues.saveCorrection")}
														</button>
														<button
															type="button"
															className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink"
															onClick={card.onCancel}
														>
															{t("typeAheadValues.cancel")}
														</button>
													</>
												) : (
													<>
														<button
															type="button"
															className="touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600"
															onClick={card.onApprove}
														>
															{t("typeAheadValues.approve")}
														</button>
														<button
															type="button"
															className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink"
															onClick={card.onCorrect}
														>
															{t("typeAheadValues.correct")}
														</button>
														{!value.isRemoved && (
															<button
																type="button"
																className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink"
																onClick={card.onRemove}
															>
																{t("typeAheadValues.remove")}
															</button>
														)}
														{value.mergeTargets.length > 0 && (
															<span className="flex flex-wrap items-center gap-2">
																<label className="font-sans text-sm text-ink">
																	<span className="sr-only">{t("typeAheadValues.mergeInto")}</span>
																	<select
																		aria-label={t("typeAheadValues.mergeInto")}
																		className="touch-target rounded border border-rule bg-surface-2 px-3 font-sans text-ink"
																		value={card.mergeSelected}
																		onChange={(event) => card.onMergeInto(event.target.value)}
																	>
																		<option value="">{t("typeAheadValues.mergeInto")}</option>
																		{card.mergeOptions.map((target) => (
																			<option key={target.id} value={target.id}>
																				{target.text}
																			</option>
																		))}
																	</select>
																</label>
																<button
																	type="button"
																	disabled={!card.mergeSelected}
																	className="touch-target inline-flex items-center rounded border border-rule px-4 font-sans text-ink disabled:opacity-50"
																	onClick={card.onMerge}
																>
																	{t("typeAheadValues.merge")}
																</button>
															</span>
														)}
													</>
												)}
											</div>
										</li>
									)
								})}
							</ul>
						</section>
					))}
				</div>
			)}
		</main>
	)
}
