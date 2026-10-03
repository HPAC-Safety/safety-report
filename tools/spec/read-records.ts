// What each decision record and lesson cites, read from its own file, so
// .spec/claims.json can say which ADRs and lessons name each claim and the
// graph fragment can link them (ADR-0084, ADR-0085).
//
// A record cites every claim, constraint, and other ADR it names anywhere in
// its text. Only its status paragraph says how it relates to another ADR —
// superseding or amending it — because the rest of a record discusses other
// records freely.
//
// Dependency-free, like every other tool in this directory.
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'

import { parseFrontmatter } from '../docs/check-frontmatter.ts'
import { DECISIONS, LESSONS } from './spec-paths.ts'

/** The IDs a record names. */
export interface Citations {
	claims: string[]
	constraints: string[]
	decisions: string[]
}

/** One architecture decision record. */
export interface DecisionRecord extends Citations {
	id: string
	file: string
	title: string
	status: string
	/** The ADRs this one supersedes or narrows, in whole or in part. */
	supersedes: string[]
	/** The ADRs this one amends or extends. */
	amends: string[]
}

/** One lesson. */
export interface LessonRecord extends Citations {
	id: string
	file: string
	title: string
	status: string
	/** The skills its `## Skill` section names. */
	skills: string[]
}

const unique = (values: Iterable<string>): string[] => [...new Set(values)].sort()

/** The frontmatter value of `key`, quotes removed. */
function meta(text: string, key: string): string {
	const { entries = [] } = parseFrontmatter(text)
	return (entries.find((entry) => entry.key === key)?.value ?? '').replace(/^(["'])(.*)\1$/, '$2')
}

/** Every claim, constraint, and ADR `text` names, except `self`. */
export function citations(text: string, self = ''): Citations {
	return {
		claims: unique(text.match(/REQ-[A-Z]+-\d{3}/g) ?? []),
		constraints: unique(text.match(/CON-[A-Z]+-\d{3}/g) ?? []),
		decisions: unique((text.match(/ADR-\d{4}/g) ?? []).filter((id) => id !== self)),
	}
}

/**
 * A record's own status paragraph: the `**Status:**` paragraph, or the first
 * paragraph of a `## Status` section.
 */
export function statusParagraph(text: string): string {
	const bold = text.match(/^\*\*Status:\*\*[\s\S]*?(?=\n\s*\n|(?![\s\S]))/m)
	if (bold) return bold[0]
	const section = text.match(/^## Status\s*\n+([\s\S]*?)(?=\n\s*\n|^## |(?![\s\S]))/m)
	return section ? section[1] : ''
}

/** One relation between two ADRs: `from` supersedes or amends `to`. */
export interface Relation {
	from: string
	type: 'supersedes' | 'amends'
	to: string
}

const VERBS: Readonly<Record<string, { type: Relation['type']; passive: boolean }>> = {
	superseded: { type: 'supersedes', passive: true },
	narrowed: { type: 'supersedes', passive: true },
	amended: { type: 'amends', passive: true },
	extended: { type: 'amends', passive: true },
	supersedes: { type: 'supersedes', passive: false },
	narrows: { type: 'supersedes', passive: false },
	amends: { type: 'amends', passive: false },
	extends: { type: 'amends', passive: false },
}

/**
 * The relations a record's status paragraph states. Each ADR it names takes
 * the nearest verb before it in the same sentence: "Superseded by ADR-0002"
 * makes ADR-0002 supersede this record, "Amends ADR-0001" makes this record
 * amend ADR-0001. An ADR with no verb before it is only cited.
 */
export function relations(self: string, paragraph: string): Relation[] {
	const found: Relation[] = []
	let verb: (typeof VERBS)[string] | null = null
	for (const match of paragraph.matchAll(/\b(superseded|narrowed|amended|extended|supersedes|narrows|amends|extends)\b|(ADR-\d{4})|\.(?=\s|$)/gi)) {
		if (match[1]) verb = VERBS[match[1].toLowerCase()]
		else if (match[2]) {
			if (verb && match[2] !== self) found.push(verb.passive ? { from: match[2], type: verb.type, to: self } : { from: self, type: verb.type, to: match[2] })
		} else verb = null
	}
	return found
}

/** Every `ADR-NNNN-*.md` and lesson `NNNN-*.md` in a directory, in number order. */
function numbered(root: string, directory: string, pattern: RegExp): { file: string; number: string; text: string }[] {
	if (!existsSync(join(root, directory))) return []
	return readdirSync(join(root, directory))
		.flatMap((name) => {
			const number = pattern.exec(name)?.[1]
			return number ? [{ name, number }] : []
		})
		.sort((a, b) => a.number.localeCompare(b.number) || a.name.localeCompare(b.name))
		.map(({ name, number }) => {
			const file = `${directory}/${name}`
			return { file, number, text: readFileSync(join(root, file), 'utf8') }
		})
}

/** The section under `## heading`, up to the next `## `. */
function section(text: string, heading: string): string {
	return text.match(new RegExp(`^## ${heading}\\s*$([\\s\\S]*?)(?=^## |(?![\\s\\S]))`, 'm'))?.[1] ?? ''
}

/** Every decision record, with what it cites and how it relates to the others. */
export function readDecisions(root: string): DecisionRecord[] {
	const files = numbered(root, DECISIONS, /^ADR-(\d{4})-.+\.md$/)
	const ids = new Set(files.map(({ number }) => `ADR-${number}`))
	const all = files.flatMap(({ number, text }) => relations(`ADR-${number}`, statusParagraph(text))).filter(({ from, to }) => ids.has(from) && ids.has(to))
	return files.map(({ file, number, text }) => {
		const id = `ADR-${number}`
		const own = (type: Relation['type']): string[] => unique(all.filter((relation) => relation.from === id && relation.type === type).map((relation) => relation.to))
		const supersedes = own('supersedes')
		return {
			id,
			file,
			title: meta(text, 'title'),
			status: meta(text, 'status'),
			...citations(text, id),
			supersedes,
			amends: own('amends').filter((to) => !supersedes.includes(to)),
		}
	})
}

/** Every lesson, with what it cites and the skills it changed. */
export function readLessons(root: string): LessonRecord[] {
	return numbered(root, LESSONS, /^(\d{4})-.+\.md$/).map(({ file, number, text }) => ({
		id: number,
		file,
		title: meta(text, 'title'),
		status: meta(text, 'status'),
		...citations(text),
		skills: unique([...section(text, 'Skill').matchAll(/`([a-z0-9]+(?:-[a-z0-9]+)+)`/g)].map((match) => match[1])),
	}))
}
