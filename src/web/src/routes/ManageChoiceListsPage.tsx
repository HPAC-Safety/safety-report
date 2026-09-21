import { useCallback, useEffect, useState } from "react"
import { useLocale } from "../i18n/useLocale"
import {
	ApiError,
	createOptionSet,
	deleteOptionSet,
	listOptionSets,
	replaceOptionSet,
	type OptionSetView,
	type SaveOptionSetRequest,
} from "../api/adminQuestions"

/*
 * The shared choice lists behind type-ahead and picker questions.
 *
 * This screen exists mostly for curation. A reporter who flies at a site
 * nobody has written down types it into a type-ahead, and it lands here marked
 * as reporter-added, with its French machine-drafted at submission (ADR-0063).
 * A safety officer then fixes the spelling, corrects the French, merges a
 * duplicate, or removes it — which is what the marker is for.
 *
 * Saving replaces the list with exactly what is on screen: an item left out is
 * retired. Retiring is a soft delete, so every question revision that already
 * snapshotted that choice keeps its own copy and no past report changes.
 */

interface ListDraft extends SaveOptionSetRequest {
	id: string | null
}

function draftOf(set: OptionSetView): ListDraft {
	return {
		id: set.id,
		key: set.key,
		nameEn: set.nameEn,
		nameFr: set.nameFr,
		items: set.items.map((item) => ({ code: item.code, labelEn: item.labelEn, labelFr: item.labelFr })),
	}
}

export function ManageChoiceListsPage() {
	const { t } = useLocale()
	const [sets, setSets] = useState<OptionSetView[]>([])
	const [draft, setDraft] = useState<ListDraft | null>(null)
	const [error, setError] = useState<string | null>(null)
	const [loading, setLoading] = useState(true)

	const report = useCallback(
		(cause: unknown) => setError(cause instanceof ApiError ? cause.detail : t("choiceLists.error.unexpected")),
		[t],
	)

	const load = useCallback(async () => {
		try {
			setLoading(true)
			setSets(await listOptionSets())
			setError(null)
		} catch (cause) {
			report(cause)
		} finally {
			setLoading(false)
		}
	}, [report])

	useEffect(() => {
		void load()
	}, [load])

	async function save(value: ListDraft) {
		try {
			if (value.id) {
				await replaceOptionSet(value.id, value)
			} else {
				await createOptionSet(value)
			}

			setDraft(null)
			await load()
		} catch (cause) {
			report(cause)
		}
	}

	async function remove(set: OptionSetView) {
		try {
			await deleteOptionSet(set.id)
			await load()
		} catch (cause) {
			report(cause)
		}
	}

	const reporterAdded = sets.reduce(
		(total, set) => total + set.items.filter((item) => item.addedByReporter).length,
		0,
	)

	return (
		<main className="mx-auto max-w-4xl px-6 py-12">
			<h1 className="font-display text-3xl font-bold">{t("choiceLists.title")}</h1>
			<p className="mt-2 font-sans text-ink-muted">{t("choiceLists.intro")}</p>

			{error && (
				<p role="alert" className="mt-6 rounded border border-brand-700 bg-surface-2 p-4 font-sans text-ink">
					{error}
				</p>
			)}

			{reporterAdded > 0 && (
				<p className="mt-6 rounded border border-rule bg-surface-2 p-4 font-sans text-sm text-ink">
					{t("choiceLists.reporterAddedCount", { count: String(reporterAdded) })}
				</p>
			)}

			<div className="mt-8">
				<button
					type="button"
					className="touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600"
					onClick={() => setDraft({ id: null, key: "", nameEn: "", nameFr: "", items: [] })}
				>
					{t("choiceLists.addList")}
				</button>
			</div>

			{draft && <ListEditor draft={draft} onChange={setDraft} onCancel={() => setDraft(null)} onSave={save} />}

			{loading ? (
				<p className="mt-8 font-sans text-ink-muted">{t("choiceLists.loading")}</p>
			) : sets.length === 0 ? (
				<p className="mt-8 font-sans text-ink-muted">{t("choiceLists.empty")}</p>
			) : (
				<ul aria-label={t("choiceLists.title")} className="mt-8 flex flex-col gap-4">
					{sets.map((set) => (
						<li key={set.id} className="rounded border border-rule bg-surface p-4">
							<div className="flex flex-wrap items-start justify-between gap-4">
								<div>
									<p className="font-sans font-medium text-ink">{set.nameEn}</p>
									<p className="font-sans text-sm text-ink-muted">{set.nameFr}</p>
									<p className="mt-1 font-sans text-xs text-ink-muted">
										{t("choiceLists.itemCount", { count: String(set.items.length) })}
									</p>
								</div>

								<div className="flex gap-2">
									<button
										type="button"
										className="touch-target inline-flex items-center rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2"
										onClick={() => setDraft(draftOf(set))}
									>
										{t("choiceLists.edit")}
									</button>
									<button
										type="button"
										className="touch-target inline-flex items-center rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2"
										onClick={() => void remove(set)}
									>
										{t("choiceLists.delete")}
									</button>
								</div>
							</div>

							<ul className="mt-3 flex flex-wrap gap-2">
								{set.items.map((item) => (
									<li
										key={item.code}
										className="rounded border border-rule bg-surface-2 px-3 py-1 font-sans text-xs text-ink"
									>
										{item.labelEn}
										{item.addedByReporter && (
											<span className="ml-2 rounded bg-surface-4 px-2 py-0.5">
												{t("choiceLists.reporterAdded")}
											</span>
										)}
									</li>
								))}
							</ul>
						</li>
					))}
				</ul>
			)}
		</main>
	)
}

const fieldClassName =
	"mt-1 w-full rounded border border-rule bg-surface px-3 py-2 font-sans text-ink placeholder:text-ink-muted"

const labelClassName = "block font-sans text-sm font-medium text-ink"

function ListEditor({
	draft,
	onChange,
	onCancel,
	onSave,
}: {
	draft: ListDraft
	onChange: (draft: ListDraft) => void
	onCancel: () => void
	onSave: (draft: ListDraft) => void
}) {
	const { t } = useLocale()

	function update(changes: Partial<ListDraft>) {
		onChange({ ...draft, ...changes })
	}

	function updateItem(index: number, changes: Partial<{ code: string; labelEn: string; labelFr: string }>) {
		update({ items: draft.items.map((item, current) => (current === index ? { ...item, ...changes } : item)) })
	}

	return (
		<form
			className="mt-6 flex flex-col gap-5 rounded border border-rule bg-surface-2 p-6"
			onSubmit={(event) => {
				event.preventDefault()
				onSave(draft)
			}}
		>
			<h2 className="font-display text-xl font-bold">
				{draft.id ? t("choiceLists.editorTitleEdit") : t("choiceLists.editorTitleNew")}
			</h2>

			<div className="grid gap-4 sm:grid-cols-3">
				<div>
					<label className={labelClassName} htmlFor="list-key">
						{t("choiceLists.field.key")}
					</label>
					<input
						id="list-key"
						className={fieldClassName}
						value={draft.key ?? ""}
						readOnly={draft.id !== null}
						required
						onChange={(event) => update({ key: event.target.value })}
					/>
				</div>

				<div>
					<label className={labelClassName} htmlFor="list-name-en">
						{t("choiceLists.field.nameEn")}
					</label>
					<input
						id="list-name-en"
						className={fieldClassName}
						value={draft.nameEn}
						required
						onChange={(event) => update({ nameEn: event.target.value })}
					/>
				</div>

				<div>
					<label className={labelClassName} htmlFor="list-name-fr">
						{t("choiceLists.field.nameFr")}
					</label>
					<input
						id="list-name-fr"
						className={fieldClassName}
						value={draft.nameFr}
						required
						onChange={(event) => update({ nameFr: event.target.value })}
					/>
				</div>
			</div>

			<h3 className="font-sans text-sm font-medium text-ink">{t("choiceLists.field.items")}</h3>
			<p className="font-sans text-xs text-ink-muted">{t("choiceLists.field.itemsHelp")}</p>

			{draft.items.map((item, index) => (
				<div key={index} className="grid gap-2 sm:grid-cols-3">
					<input
						className={fieldClassName}
						value={item.code}
						required
						aria-label={t("choiceLists.field.itemCode")}
						placeholder={t("choiceLists.field.itemCode")}
						onChange={(event) => updateItem(index, { code: event.target.value })}
					/>
					<input
						className={fieldClassName}
						value={item.labelEn}
						required
						aria-label={t("choiceLists.field.itemLabelEn")}
						placeholder={t("choiceLists.field.itemLabelEn")}
						onChange={(event) => updateItem(index, { labelEn: event.target.value })}
					/>
					<div className="flex gap-2">
						<input
							className={fieldClassName}
							value={item.labelFr}
							required
							aria-label={t("choiceLists.field.itemLabelFr")}
							placeholder={t("choiceLists.field.itemLabelFr")}
							onChange={(event) => updateItem(index, { labelFr: event.target.value })}
						/>
						<button
							type="button"
							className="touch-target rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface"
							aria-label={t("choiceLists.field.removeItem")}
							onClick={() => update({ items: draft.items.filter((_, current) => current !== index) })}
						>
							×
						</button>
					</div>
				</div>
			))}

			<button
				type="button"
				className="touch-target self-start rounded border border-rule px-4 font-sans text-sm text-ink hover:bg-surface"
				onClick={() => update({ items: [...draft.items, { code: "", labelEn: "", labelFr: "" }] })}
			>
				{t("choiceLists.field.addItem")}
			</button>

			<div className="flex gap-3">
				<button
					type="submit"
					className="touch-target inline-flex items-center rounded bg-brand-700 px-5 font-sans font-medium text-ink-inverse hover:bg-brand-600"
				>
					{t("choiceLists.save")}
				</button>
				<button
					type="button"
					className="touch-target inline-flex items-center rounded border border-rule px-5 font-sans text-ink hover:bg-surface"
					onClick={onCancel}
				>
					{t("choiceLists.cancel")}
				</button>
			</div>
		</form>
	)
}
