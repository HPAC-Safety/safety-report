/*
 * Typeform question-bank import (ADR-0077, ADR-0078).
 *
 * Import takes an English + French export pair and returns drafts for the
 * administrator to review one at a time in the ordinary QuestionEditor —
 * nothing here saves a Question. Any real branching logic Typeform's `logic[]`
 * expressed is deferred to a pending-logic note instead of being guessed at.
 */

import { authorization, ApiError, type QuestionType } from "./adminQuestions"
import { clearSession } from "../auth/session"

export interface ImportedOptionView {
	code: string
	labelEn: string
	labelFr: string
	frenchDefaultedToEnglish: boolean
}

export interface ImportedQuestionDraftView {
	key: string
	type: QuestionType
	labelEn: string
	labelFr: string
	frenchDefaultedToEnglish: boolean
	helpTextEn: string | null
	helpTextFr: string | null
	groupedUnderKey: string | null
	allowsReporterAdditions: boolean
	options: ImportedOptionView[]
}

export interface RejectedTypeformFieldView {
	ref: string
	title: string
	typeformType: string
}

export interface TypeformImportPreviewResponse {
	drafts: ImportedQuestionDraftView[]
	rejected: RejectedTypeformFieldView[]
	pendingLogicNoteIds: string[]
}

export interface PendingImportLogicView {
	id: string
	fieldRef: string
	fieldTitle: string
	rawLogicJson: string
	createdAt: string
}

/**
 * The one call in this module that isn't plain JSON: the browser must set its
 * own `multipart/form-data; boundary=...` Content-Type, so this does not go
 * through the JSON `call` helper other admin clients share.
 */
export async function importTypeform(english: File, french: File): Promise<TypeformImportPreviewResponse> {
	const body = new FormData()
	body.append("english", english)
	body.append("french", french)

	const response = await fetch("/api/admin/typeform/import", {
		method: "POST",
		headers: authorization(),
		body,
	})

	if (response.status === 401) {
		clearSession()
	}

	if (!response.ok) {
		const problem = await response.json().catch(() => null)
		throw new ApiError(response.status, problem?.detail ?? problem?.title ?? response.statusText)
	}

	return (await response.json()) as TypeformImportPreviewResponse
}

export async function listPendingImportLogic(): Promise<PendingImportLogicView[]> {
	const response = await fetch("/api/admin/typeform/pending-logic", { headers: authorization() })

	if (!response.ok) {
		const problem = await response.json().catch(() => null)
		throw new ApiError(response.status, problem?.detail ?? problem?.title ?? response.statusText)
	}

	return (await response.json()) as PendingImportLogicView[]
}

export async function deletePendingImportLogic(id: string): Promise<void> {
	const response = await fetch(`/api/admin/typeform/pending-logic/${id}`, {
		method: "DELETE",
		headers: authorization(),
	})

	if (!response.ok) {
		const problem = await response.json().catch(() => null)
		throw new ApiError(response.status, problem?.detail ?? problem?.title ?? response.statusText)
	}
}
