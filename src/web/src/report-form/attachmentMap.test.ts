import { describe, expect, it } from "vitest"

import type { Attachment } from "./AttachmentField"
import { hasFileAttached, markRejectedUploads, savedAttachments } from "./attachmentMap"

const uploaded = (uploadId: string, name = `${uploadId}.pdf`): Attachment => ({ key: `k-${uploadId}`, name, size: 10, status: "uploaded", uploadId })

describe("hasFileAttached", () => {
	it("is false with no attachments at all", () => {
		expect(hasFileAttached({})).toBe(false)
	})

	it("is false while every row is refused or expired", () => {
		const rows: Attachment[] = [
			{ key: "a", name: "a", size: 1, status: "rejected", reason: "network" },
			{ key: "b", name: "b", size: 1, status: "expired", uploadId: "u" },
		]
		expect(hasFileAttached({ q: rows })).toBe(false)
	})

	it("is true when any question holds a finished upload", () => {
		expect(hasFileAttached({ one: [], two: [uploaded("u1")] })).toBe(true)
	})
})

describe("savedAttachments", () => {
	it("keeps only finished uploads that have an upload ID, per question", () => {
		const rows: Attachment[] = [
			uploaded("u1", "one.pdf"),
			{ key: "x", name: "no-id", size: 1, status: "uploaded" },
			{ key: "y", name: "refused", size: 1, status: "rejected", uploadId: "u2", reason: "unknown" },
		]
		expect(savedAttachments({ q1: rows, q2: [], q3: [{ key: "z", name: "z", size: 1, status: "expired", uploadId: "u3" }] })).toEqual({
			q1: [{ uploadId: "u1", name: "one.pdf", size: 10 }],
		})
	})
})

describe("markRejectedUploads", () => {
	it("marks expired and refused uploads and leaves every other row as it was", () => {
		const untouched = uploaded("ok")
		const withoutId: Attachment = { key: "n", name: "n", size: 1, status: "rejected", reason: "network" }
		const result = markRejectedUploads(
			{ q1: [uploaded("gone"), uploaded("bad"), untouched, withoutId], q2: [] },
			new Set(["gone"]),
			new Map([["bad", "unaccepted_media_type" as const]]),
		)

		expect(result.q1[0]).toMatchObject({ uploadId: "gone", status: "expired" })
		expect(result.q1[1]).toMatchObject({ uploadId: "bad", status: "rejected", reason: "unaccepted_media_type" })
		expect(result.q1[2]).toBe(untouched)
		expect(result.q1[3]).toBe(withoutId)
		expect(result.q2).toEqual([])
	})
})
