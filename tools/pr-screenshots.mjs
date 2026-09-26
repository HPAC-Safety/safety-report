#!/usr/bin/env node
// A pull request that changes a rendered web file shows its screenshots, or
// says why it has none (ADR-0142).
//
// The check reads only the pull-request body and the changed-file list. It
// cannot judge whether the shots are the right ones or whether a before/after
// pair is complete — review does that. What it can do is refuse a web change
// that reaches review with neither a screenshot nor a reason for its absence:
//
//     ![after](https://raw.githubusercontent.com/<owner>/<repo>/<sha>/docs/screenshots/<dir>/after-form.png)
//
// or, for a change with nothing visible:
//
//     No screenshot needed: <reason>
//
//   PR_BODY=… [CHANGED_FILES=…] [GITHUB_REPOSITORY=owner/repo] node tools/pr-screenshots.mjs
//
// With no CHANGED_FILES, the changed files are this branch's against
// origin/main, so a local run checks what CI will (ADR-0073). With no PR_BODY
// there is nothing to check, and the run fails rather than pass silently.
//
// The exit code is the contract.
import { execFileSync } from 'node:child_process'

// A file whose change can alter what a page looks like: a component or a
// stylesheet under the web source tree. A test beside it renders nothing.
const RENDERED = /^src\/web\/src\/.+\.(?:tsx|css)$/
const TEST = /\.test\.[^/]+$/

const EXEMPTION = /^No screenshot needed:[ \t]*(.*)$/im

// A reason has to carry information, as the feature-coverage exemption's does.
const MINIMUM_REASON_WORDS = 4

const escape = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

/**
 * An image of a file committed under docs/screenshots/, linked by a raw URL
 * pinned to a commit — the only form that renders in a pull request (lesson
 * 0017) — as a Markdown image or an `<img>` tag. `repository` is `owner/repo`
 * when known; otherwise any repository matches.
 */
export function screenshotPattern(repository) {
	const repo = repository ? escape(repository) : '[^/\\s]+/[^/\\s]+'
	const url = `https://raw\\.githubusercontent\\.com/${repo}/[0-9a-f]{7,40}/(?:[^\\s)"'>]+/)?docs/screenshots/[^\\s)"'>]+\\.(?:png|jpe?g|gif|webp)`
	return new RegExp(`!\\[[^\\]]*\\]\\(\\s*${url}|<img\\b[^>]*\\bsrc=["']?${url}`, 'gi')
}

// The template's guidance sits in HTML comments, and an example sits in a code
// block. Left in a body, neither counts as a screenshot or an exemption. An
// unclosed comment or fence hides everything after it, as it does when rendered.
const withoutExamples = (body) =>
	body.replace(/<!--[\s\S]*?(?:-->|$)/g, '').replace(/^(```|~~~)[\s\S]*?(?:^\1[^\n]*$|(?![\s\S]))/gm, '')

/** The changed files that render in the web app. */
export function renderedFiles(changed) {
	return changed.filter((path) => RENDERED.test(path) && !TEST.test(path))
}

/**
 * The verdict for one pull request. `changed` is every file it touched;
 * `repository` is `owner/repo`, or undefined to accept any.
 */
export function judge({ changed, body, repository }) {
	const rendered = renderedFiles(changed)
	if (rendered.length === 0) return { ok: true, note: 'No rendered web file changed.' }

	const text = withoutExamples(body)
	const shots = text.match(screenshotPattern(repository)) ?? []
	if (shots.length > 0) return { ok: true, note: `Screenshots linked: ${shots.length}.` }

	const exemption = text.match(EXEMPTION)
	if (!exemption) return { ok: false, rendered, problems: [] }

	const reason = exemption[1].trim()
	if (reason.startsWith('<')) {
		return { ok: false, rendered, problems: [`"${reason}" is the template's placeholder — replace it with the reason.`] }
	}
	if (reason.split(/\s+/).filter(Boolean).length < MINIMUM_REASON_WORDS) {
		return {
			ok: false,
			rendered,
			problems: [`"${reason}" is not a reason — say what changed and why nothing on screen did.`],
		}
	}

	return { ok: true, note: `No screenshot needed: ${reason}` }
}

const lines = (value) => value.split('\n').map((line) => line.trim()).filter(Boolean)

const branchChanges = () => execFileSync('git', ['diff', '--name-only', 'origin/main...HEAD'], { encoding: 'utf8' })

/**
 * The check's input from the environment, or `{ error }` when there is nothing
 * to check. `diff` supplies the changed files when CHANGED_FILES is unset.
 */
export function readInput(env, diff = branchChanges) {
	if (env.PR_BODY === undefined) {
		return { error: 'PR_BODY is not set, so there is no body to check. Usage: PR_BODY="$(cat pr-body.md)" node tools/pr-screenshots.mjs' }
	}
	return {
		changed: lines(env.CHANGED_FILES ?? diff()),
		body: env.PR_BODY,
		repository: env.GITHUB_REPOSITORY || undefined,
	}
}

/**
 * Reports the way the command line does, without exiting, so a test can
 * exercise every outcome in-process. Returns the exit code.
 */
export function main(input) {
	if (input.error) {
		console.error(`::error::${input.error}`)
		return 1
	}

	const verdict = judge(input)

	if (verdict.ok) {
		console.log(`::notice::${verdict.note}`)
		return 0
	}

	for (const problem of verdict.problems) console.error(`::error::${problem}`)
	console.error('::error::This pull request changes a rendered web file and shows no screenshot.')
	console.error('')
	console.error('Changed files that render:')
	for (const path of verdict.rendered) console.error(`  ${path}`)
	console.error('')
	console.error('Commit the shots under docs/screenshots/<dir>/ — before-* and after-* for a')
	console.error('changed page, after-* for a new one — and show each in the body as an image')
	console.error('linked by a raw.githubusercontent.com URL pinned to the commit that added it.')
	console.error('')
	console.error('If nothing on screen changed, say so on its own line:')
	console.error('')
	console.error('    No screenshot needed: <what changed, and why nothing visible did>')
	console.error('')
	console.error('See ADR-0142.')
	return 1
}

const runAsCommand = String(process.argv[1]).endsWith('pr-screenshots.mjs')
if (runAsCommand) process.exit(main(readInput(process.env)))
