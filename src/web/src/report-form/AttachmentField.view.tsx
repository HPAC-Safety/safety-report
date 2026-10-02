import { AttachmentDropZone } from "../components/AttachmentDropZone"
import type { AttachmentFieldModel, AttachmentFieldProps } from "./AttachmentField"
import { formatFileSize } from "./formatFileSize"

export type AttachmentFieldViewProps = AttachmentFieldProps & AttachmentFieldModel

/** A file-upload question's attachments: the drop zone, and a row for each file (ADR-0096, ADR-0126). */
export function AttachmentFieldView({
	fieldId,
	describedBy,
	attachments,
	t,
	inFlight,
	hasRows,
	guidanceId,
	guidance,
	accept,
	rowMessage,
	onFiles,
	onCancel,
	onRemove,
}: AttachmentFieldViewProps) {
	return (
		<>
			<AttachmentDropZone
				fieldId={fieldId}
				describedBy={describedBy}
				guidanceId={guidanceId}
				guidance={guidance}
				promptText={t("report.attachments.dropPrompt")}
				accept={accept}
				onFiles={onFiles}
			/>

			{hasRows && (
				<ul className="mt-3 divide-y divide-rule rounded border border-rule" aria-label={t("report.attachments.listLabel")}>
					{attachments.map((row) => (
						<li key={row.key} className="flex items-center justify-between gap-3 px-3 py-2">
							<div className="min-w-0">
								<p className="truncate font-sans text-sm text-ink">{row.name}</p>
								<p className="font-sans text-xs text-ink-muted">{formatFileSize(row.size)}</p>
								{row.status !== "uploaded" && (
									<p role="alert" className="font-sans text-xs text-brand-700">
										{rowMessage(row)}
									</p>
								)}
							</div>
							<button
								type="button"
								className="touch-target shrink-0 rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2"
								aria-label={t("report.attachments.removeNamed", { name: row.name })}
								onClick={() => onRemove(row)}
							>
								{t("report.attachments.remove")}
							</button>
						</li>
					))}
					{inFlight.map((upload) => (
						<li key={upload.key} className="flex items-center justify-between gap-3 px-3 py-2" aria-busy="true">
							<div className="flex min-w-0 items-center gap-3">
								<span
									role="progressbar"
									aria-label={t("report.attachments.uploadingNamed", { name: upload.name })}
									className="inline-block h-4 w-4 shrink-0 animate-spin rounded-full border-2 border-rule border-t-brand-700 motion-reduce:animate-none"
								/>
								<div className="min-w-0">
									<p className="truncate font-sans text-sm text-ink">{upload.name}</p>
									<p className="font-sans text-xs text-ink-muted">{t("report.attachments.uploading")}</p>
								</div>
							</div>
							<button
								type="button"
								className="touch-target shrink-0 rounded border border-rule px-3 font-sans text-sm text-ink hover:bg-surface-2"
								aria-label={t("report.attachments.cancelNamed", { name: upload.name })}
								onClick={() => onCancel(upload.key)}
							>
								{t("report.attachments.cancel")}
							</button>
						</li>
					))}
				</ul>
			)}
		</>
	)
}
