import { useLocale } from "../i18n/useLocale"
import { AttachmentDropZone } from "./AttachmentDropZone"
import type { PrivateAttachmentStagingModel, PrivateAttachmentStagingProps } from "./PrivateAttachmentStaging"
import { PRIMARY } from "./privateAttachmentStyles"

export type PrivateAttachmentStagingViewProps = PrivateAttachmentStagingProps & PrivateAttachmentStagingModel

/** The staging area: a drop zone, one row per staged file with its progress, error or description, and Add all. */
export function PrivateAttachmentStagingView({
	rows,
	adding,
	problem,
	hasStaged,
	formatSize,
	maxSize,
	descriptionMaxLength,
	addDisabled,
	addAllLabel,
	onFiles,
	onRowAction,
	onDescription,
	onAddAll,
}: PrivateAttachmentStagingViewProps) {
	const { t } = useLocale()

	return (
		<div className="mt-4">
			<label htmlFor="private-attachment-input" className="font-sans text-sm font-medium text-ink">
				{t("privateAttachments.fileLabel")}
			</label>
			<AttachmentDropZone
				fieldId="private-attachment-input"
				describedBy={undefined}
				guidanceId="private-attachment-guidance"
				guidance={t("privateAttachments.limit", { max: maxSize })}
				promptText={t("privateAttachments.dropPrompt")}
				onFiles={onFiles}
			/>

			{hasStaged && (
				<ul className="mt-3 divide-y divide-rule rounded border border-rule" aria-label={t("privateAttachments.stagedListLabel")}>
					{rows.map((row) => (
						<li key={row.key} className="flex flex-col gap-2 px-3 py-3">
							<div className="flex items-center justify-between gap-3">
								<div className="min-w-0">
									<p className="truncate font-sans text-sm text-ink">{row.name}</p>
									<p className="font-sans text-xs text-ink-muted">{formatSize(row.size)}</p>
								</div>
								<button
									type="button"
									className="touch-target shrink-0 rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2 disabled:opacity-50"
									disabled={adding}
									aria-label={row.status === "uploading" ? t("privateAttachments.cancelNamed", { name: row.name }) : t("privateAttachments.removeStagedNamed", { name: row.name })}
									onClick={() => onRowAction(row)}
								>
									{row.status === "uploading" ? t("privateAttachments.cancelLabel") : t("privateAttachments.remove")}
								</button>
							</div>

							{row.status === "uploading" && (
								<div className="flex flex-wrap items-center gap-3" aria-busy="true">
									<progress
										max={1}
										value={row.progress}
										aria-label={t("privateAttachments.uploadingNamed", { name: row.name })}
										className="h-2 w-full max-w-xs"
									/>
									<span className="font-sans text-xs text-ink-muted" aria-live="polite">
										{t("privateAttachments.progress", { percent: String(Math.round(row.progress * 100)) })}
									</span>
								</div>
							)}

							{row.status === "rejected" && (
								<p role="alert" className="font-sans text-xs text-brand-700">
									{row.reason === "too_large" || row.reason === "empty"
										? t(`privateAttachments.error.${row.reason}`, { max: maxSize })
										: t("privateAttachments.error.upload")}
								</p>
							)}

							{row.status === "uploaded" && (
								<div>
									<label className="font-sans text-sm text-ink">
										{t("privateAttachments.descriptionLabel")}
										<textarea
											value={row.description}
											disabled={adding}
											rows={2}
											onChange={(event) => onDescription(row.key, event.target.value)}
											className="mt-1 block w-full rounded border border-rule bg-surface p-2 font-sans text-sm font-normal text-ink"
										/>
									</label>
									<p
										className={`mt-1 font-sans text-xs ${
											row.description.trim().length > descriptionMaxLength ? "text-brand-700" : "text-ink-muted"
										}`}
										aria-live="polite"
									>
										{t("privateAttachments.descriptionLength", {
											count: String(row.description.trim().length),
											max: String(descriptionMaxLength),
										})}
									</p>
								</div>
							)}
						</li>
					))}
				</ul>
			)}

			{problem && (
				<p role="alert" className="mt-2 font-sans text-sm text-brand-700">
					{problem}
				</p>
			)}

			{hasStaged && (
				<div className="mt-3">
					<button type="button" disabled={addDisabled} onClick={onAddAll} className={PRIMARY}>
						{addAllLabel}
					</button>
				</div>
			)}
		</div>
	)
}
