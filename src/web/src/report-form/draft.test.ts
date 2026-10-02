import { afterEach, beforeEach, describe, expect, it, vi } from "vitest"
import { clearDraft, draftExpiresAtMs, draftUploadIds, readDraft, writeDraft, type ReportDraft } from "./draft"

const KEY = "hpac.report.draft"
const DAY = 24 * 60 * 60 * 1000
const NOW = new Date("2026-03-01T12:00:00Z").getTime()

function baseDraft(): Omit<ReportDraft, "savedAtMs" | "startedAtMs"> {
	return { locale: "en-CA", answers: { r1: { kind: "value", value: "hello" } } }
}

function store(draft: Partial<ReportDraft>): void {
	localStorage.setItem(KEY, JSON.stringify({ locale: "en-CA", answers: {}, savedAtMs: NOW, ...draft }))
}

describe("draft storage", () => {
	beforeEach(() => {
		localStorage.clear()
		vi.useFakeTimers()
		vi.setSystemTime(NOW)
	})

	afterEach(() => {
		vi.restoreAllMocks()
		vi.useRealTimers()
		localStorage.clear()
	})

	describe("draftUploadIds", () => {
		it("lists every upload across questions", () => {
			const ids = draftUploadIds({
				...baseDraft(),
				savedAtMs: NOW,
				attachments: {
					a: [{ uploadId: "u1", name: "x", size: 1 }],
					b: [
						{ uploadId: "u2", name: "y", size: 2 },
						{ uploadId: "u3", name: "z", size: 3 },
					],
				},
			})
			expect(ids).toEqual(["u1", "u2", "u3"])
		})

		it("is empty when the draft has no attachments", () => {
			expect(draftUploadIds({ ...baseDraft(), savedAtMs: NOW })).toEqual([])
		})
	})

	describe("readDraft", () => {
		it("returns no draft when nothing is stored", () => {
			expect(readDraft()).toEqual({ draft: null, expiredUploadIds: [] })
		})

		it("returns a live draft using the current time by default", () => {
			store({ startedAtMs: NOW - DAY })
			const read = readDraft()
			expect(read.draft?.locale).toBe("en-CA")
			expect(read.expiredUploadIds).toEqual([])
		})

		it("keeps a draft exactly 15 days old", () => {
			store({ startedAtMs: NOW - 15 * DAY })
			expect(readDraft(NOW).draft).not.toBeNull()
		})

		it("expires a draft older than 15 days, clears it and returns its uploads", () => {
			store({ startedAtMs: NOW - 15 * DAY - 1, attachments: { q: [{ uploadId: "u1", name: "n", size: 1 }] } })
			const read = readDraft(NOW)
			expect(read.draft).toBeNull()
			expect(read.expiredUploadIds).toEqual(["u1"])
			expect(localStorage.getItem(KEY)).toBeNull()
		})

		it("dates a draft without a start time from its last save", () => {
			store({ savedAtMs: NOW - 16 * DAY })
			expect(readDraft(NOW).draft).toBeNull()
		})

		it.each([
			["null", "null"],
			["a string", '"text"'],
			["a number", "5"],
			["a missing locale", JSON.stringify({ answers: {}, savedAtMs: 1 })],
			["a missing savedAtMs", JSON.stringify({ locale: "en-CA", answers: {} })],
			["missing answers", JSON.stringify({ locale: "en-CA", savedAtMs: 1 })],
			["null answers", JSON.stringify({ locale: "en-CA", answers: null, savedAtMs: 1 })],
		])("ignores a stored value that is %s", (_name, raw) => {
			localStorage.setItem(KEY, raw)
			expect(readDraft(NOW)).toEqual({ draft: null, expiredUploadIds: [] })
		})

		it("returns no draft for corrupt JSON", () => {
			localStorage.setItem(KEY, "{not json")
			expect(readDraft(NOW)).toEqual({ draft: null, expiredUploadIds: [] })
		})

		it("returns no draft when storage throws", () => {
			vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
				throw new Error("denied")
			})
			expect(readDraft(NOW)).toEqual({ draft: null, expiredUploadIds: [] })
		})
	})

	describe("draftExpiresAtMs", () => {
		it("is null when nothing is stored", () => {
			expect(draftExpiresAtMs()).toBeNull()
		})

		it("is 15 days after the start", () => {
			store({ startedAtMs: NOW - DAY })
			expect(draftExpiresAtMs()).toBe(NOW - DAY + 15 * DAY)
		})

		it("is null when the draft has expired", () => {
			store({ startedAtMs: NOW - 20 * DAY })
			expect(draftExpiresAtMs(NOW)).toBeNull()
		})

		it("is null when storage throws or is corrupt", () => {
			localStorage.setItem(KEY, "{bad")
			expect(draftExpiresAtMs(NOW)).toBeNull()
		})
	})

	describe("writeDraft", () => {
		it("saves a first draft starting now", () => {
			writeDraft(baseDraft())
			const saved = JSON.parse(localStorage.getItem(KEY) ?? "{}") as ReportDraft
			expect(saved.startedAtMs).toBe(NOW)
			expect(saved.savedAtMs).toBe(NOW)
			expect(saved.locale).toBe("en-CA")
		})

		it("keeps the start time of a live earlier draft", () => {
			store({ startedAtMs: NOW - 3 * DAY })
			writeDraft(baseDraft(), NOW)
			const saved = JSON.parse(localStorage.getItem(KEY) ?? "{}") as ReportDraft
			expect(saved.startedAtMs).toBe(NOW - 3 * DAY)
			expect(saved.savedAtMs).toBe(NOW)
		})

		it("starts a new window when the earlier draft has expired", () => {
			store({ startedAtMs: NOW - 30 * DAY })
			writeDraft(baseDraft(), NOW)
			const saved = JSON.parse(localStorage.getItem(KEY) ?? "{}") as ReportDraft
			expect(saved.startedAtMs).toBe(NOW)
		})

		it("starts a new window when the earlier draft is corrupt", () => {
			localStorage.setItem(KEY, "{bad")
			writeDraft(baseDraft(), NOW)
			const saved = JSON.parse(localStorage.getItem(KEY) ?? "{}") as ReportDraft
			expect(saved.startedAtMs).toBe(NOW)
		})

		it("does not throw when storage refuses the write", () => {
			vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
				throw new Error("quota")
			})
			expect(() => writeDraft(baseDraft(), NOW)).not.toThrow()
			expect(localStorage.getItem(KEY)).toBeNull()
		})
	})

	describe("clearDraft", () => {
		it("removes the saved draft", () => {
			store({})
			clearDraft()
			expect(localStorage.getItem(KEY)).toBeNull()
		})

		it("does not throw when storage throws", () => {
			vi.spyOn(Storage.prototype, "removeItem").mockImplementation(() => {
				throw new Error("denied")
			})
			expect(() => clearDraft()).not.toThrow()
		})
	})
})
