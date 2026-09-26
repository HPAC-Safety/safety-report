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
//   CHANGED_FILES=… PR_BODY=… [PR_NUMBER=…] node tools/pr-screenshots.mjs
//
// The exit code is the contract.

// The first pull request the rule applies to. Pull requests opened before the
// rule existed were written without it, so they are not failed for it.
export const FIRST_PR = 532

// A file whose change can alter what a page looks like: a component or a
// stylesheet under the web source tree. A test beside it renders nothing.
const RENDERED = /^src\/web\/src\/.+\.(?:tsx|css)$/
const TEST = /\.test\.[^/]+$/

// An image committed under docs/screenshots/ and linked by a raw URL pinned to
// a commit — the only form that renders in a pull request (lesson 0017).
const SCREENSHOT =
	/https:\/\/raw\.githubusercontent\.com\/[^/\s]+\/[^/\s]+\/[0-9a-f]{7,40}\/(?:[^\s)"']+\/)?docs\/screenshots\/[^\s)"']+\.(?:png|jpe?g|gif|webp)/i
const EXEMPTION = /^No screenshot needed:[ \t]*(.*)$/im

// A reason has to carry information, as the feature-coverage exemption's does.
const MINIMUM_REASON_WORDS = 4

// The template's guidance sits in HTML comments. Left in a body, it must not
// count as a screenshot or an exemption.
const withoutComments = (body) => body.replace(/<!--[\s\S]*?-->/g, '')

/** The changed files that render in the web app. */
export function renderedFiles(changed) {
	return changed.filter((path) => RENDERED.test(path) && !TEST.test(path))
}

/**
 * The verdict for one pull request. `changed` is every file it touched; `number`
 * is its pull-request number, or undefined when run locally.
 */
export function judge({ changed, body, number }) {
	if (number !== undefined && number < FIRST_PR) {
		return { ok: true, note: `#${number} was opened before the screenshot rule (ADR-0142).` }
	}

	const rendered = renderedFiles(changed)
	if (rendered.length === 0) return { ok: true, note: 'No rendered web file changed.' }

	const text = withoutComments(body)
	const shots = text.match(new RegExp(SCREENSHOT, 'gi')) ?? []
	if (shots.length > 0) return { ok: true, note: `Screenshots linked: ${shots.length}.` }

	const exemption = text.match(EXEMPTION)
	if (!exemption) return { ok: false, rendered, problems: [] }

	const reason = exemption[1].trim()
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

/**
 * Reports the way the command line does, without exiting, so a test can
 * exercise every outcome in-process. Returns the exit code.
 */
export function main({ changed, body, number }) {
	const verdict = judge({ changed, body, number })

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
	console.error('changed page, after-* for a new one — and link each from the body by a')
	console.error('raw.githubusercontent.com URL pinned to the commit that added it.')
	console.error('')
	console.error('If nothing on screen changed, say so on its own line:')
	console.error('')
	console.error('    No screenshot needed: <what changed, and why nothing visible did>')
	console.error('')
	console.error('See ADR-0142.')
	return 1
}

const runAsCommand = String(process.argv[1]).endsWith('pr-screenshots.mjs')
if (runAsCommand) {
	const number = Number.parseInt(process.env.PR_NUMBER ?? '', 10)
	process.exit(
		main({
			changed: lines(process.env.CHANGED_FILES ?? ''),
			body: process.env.PR_BODY ?? '',
			number: Number.isNaN(number) ? undefined : number,
		}),
	)
}
