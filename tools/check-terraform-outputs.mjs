#!/usr/bin/env node
// A workflow that reads `terraform output <name>` is trusting infra/outputs.tf
// to still declare that name. Nothing else checks that: `terraform validate`
// never sees the workflow files, and a renamed or removed output only fails at
// release time, against a real AWS account, which is exactly the run nobody
// gets to rehearse (issue #466 review, "the first release would fail").
//
// This checks three conventions this repository actually uses, not arbitrary
// HCL or arbitrary jq:
//
//   1. `terraform ... output -raw NAME` / `output -json NAME` - NAME must be a
//      top-level `output "NAME" { ... }` block in infra/outputs.tf.
//   2. `steps.tf.outputs.KEY` - read after a step (conventionally `id: tf`)
//      that ran `output -json deploy_variables` and fanned it into
//      $GITHUB_OUTPUT with `jq -r 'to_entries[] | ...'`. KEY must be a
//      top-level key of that output's `value = { ... }` map.
//   3. `output -json site_urls | jq -r '.KEY'` (or similar, read within the
//      same step) - KEY must be a top-level key of site_urls' value map.
//
// It does NOT parse Terraform generally, evaluate `for` expressions, or
// follow a value through a local. A new convention (a fourth map read a new
// way) needs a fourth check added here, not a workaround.
//
//   node tools/check-terraform-outputs.mjs
//
// The exit code is the contract.
import { globSync, readFileSync } from 'node:fs'

export const OUTPUTS_FILE = 'infra/outputs.tf'
export const WORKFLOWS_GLOB = '.github/workflows/*.yml'

/** Index of the character matching the `{` at `openIndex`, scanning forward. */
function matchBrace(text, openIndex) {
	let depth = 0
	for (let i = openIndex; i < text.length; i++) {
		if (text[i] === '{') depth++
		else if (text[i] === '}') {
			depth--
			if (depth === 0) return i
		}
	}
	throw new Error(`unbalanced braces from index ${openIndex}`)
}

/** Every `NAME =` assignment at brace/bracket/paren depth 0 within `text`. */
function shallowKeys(text) {
	const keys = new Set()
	let depth = 0
	for (const line of text.split('\n')) {
		if (depth === 0) {
			const match = /^\s*([A-Za-z0-9_]+)\s*=(?!=)/.exec(line)
			if (match) keys.add(match[1])
		}
		for (const ch of line) {
			if (ch === '{' || ch === '[' || ch === '(') depth++
			else if (ch === '}' || ch === ']' || ch === ')') depth--
		}
	}
	return keys
}

/**
 * Every top-level `output "NAME" { ... }` block in Terraform outputs.tf, as a
 * map of name to the top-level keys of its `value = { ... }` object literal
 * (empty when `value` is not an object literal - a string, a list, or a
 * `concat(...)`/`for` expression nobody reads a sub-key of by name here).
 */
export function parseOutputs(hcl) {
	const outputs = new Map()
	const outputRe = /^output\s+"([a-zA-Z0-9_]+)"\s*{/gm
	let match
	while ((match = outputRe.exec(hcl))) {
		const openIndex = hcl.indexOf('{', match.index)
		const closeIndex = matchBrace(hcl, openIndex)
		const body = hcl.slice(openIndex + 1, closeIndex)

		const valueMatch = /value\s*=\s*{/.exec(body)
		let keys = new Set()
		if (valueMatch) {
			const valueOpen = valueMatch.index + valueMatch[0].length - 1
			const valueClose = matchBrace(body, valueOpen)
			keys = shallowKeys(body.slice(valueOpen + 1, valueClose))
		}
		outputs.set(match[1], keys)
	}
	return outputs
}

/** `terraform output -raw NAME` / `-json NAME` references, top-level names only. */
export function referencedOutputNames(workflowText) {
	return new Set([...workflowText.matchAll(/terraform\s+-chdir=\S+\s+output\s+(?:-raw|-json)\s+([A-Za-z0-9_]+)/g)].map((m) => m[1]))
}

/**
 * `steps.tf.outputs.KEY` references - this repository's one convention for
 * fanning `deploy_variables` into step outputs (deploy-environment.yml's
 * "Read Terraform's deploy variables" step, `id: tf`). Assumes that id; a
 * differently-named step reading deploy_variables needs its id added here.
 */
export function referencedDeployVariableKeys(workflowText) {
	return new Set([...workflowText.matchAll(/steps\.tf\.outputs\.([A-Za-z0-9_]+)/g)].map((m) => m[1]))
}

/** `jq -r '.KEY'` read against a `site_urls` output, wherever it appears in the text. */
export function referencedSiteUrlsKeys(workflowText) {
	if (!/output\s+-json\s+site_urls/.test(workflowText)) return new Set()
	return new Set([...workflowText.matchAll(/jq\s+-r\s+'\.([A-Za-z0-9_]+)'/g)].map((m) => m[1]))
}

/** What is wrong, across every workflow file, or an empty list. */
export function problems({ outputsHcl, workflows }) {
	const outputs = parseOutputs(outputsHcl)
	const problems = []

	for (const { path, text } of workflows) {
		for (const name of referencedOutputNames(text)) {
			if (!outputs.has(name)) {
				problems.push(`${path} reads \`terraform output ${name}\`, which ${OUTPUTS_FILE} does not declare.`)
			}
		}

		if (outputs.has('deploy_variables')) {
			const keys = outputs.get('deploy_variables')
			for (const key of referencedDeployVariableKeys(text)) {
				if (!keys.has(key)) {
					problems.push(
						`${path} reads \`steps.tf.outputs.${key}\`, which is not a key of ${OUTPUTS_FILE}'s \`deploy_variables\` output.`,
					)
				}
			}
		}

		if (outputs.has('site_urls')) {
			const keys = outputs.get('site_urls')
			for (const key of referencedSiteUrlsKeys(text)) {
				if (!keys.has(key)) {
					problems.push(`${path} reads \`site_urls\`'s \`.${key}\`, which is not a key of ${OUTPUTS_FILE}'s \`site_urls\` output.`)
				}
			}
		}
	}

	return problems
}

/**
 * Reports the way the command line does, without exiting, so a test can
 * exercise every outcome in-process. Returns the exit code.
 */
export function main({ outputsHcl, workflows }) {
	const found = problems({ outputsHcl, workflows })
	if (found.length === 0) {
		console.log(`::notice::Every terraform output a workflow reads is declared in ${OUTPUTS_FILE}.`)
		return 0
	}

	for (const problem of found) console.error(`::error file=${OUTPUTS_FILE}::${problem}`)
	console.error('')
	console.error(`Add the output (or the key) to ${OUTPUTS_FILE}, or fix the workflow reference -`)
	console.error('the first release is not the place to discover a renamed or missing output.')
	return 1
}

const runAsCommand = String(process.argv[1]).endsWith('check-terraform-outputs.mjs')
if (runAsCommand) {
	const outputsHcl = readFileSync(OUTPUTS_FILE, 'utf8')
	const workflows = globSync(WORKFLOWS_GLOB).map((path) => ({ path, text: readFileSync(path, 'utf8') }))
	process.exit(main({ outputsHcl, workflows }))
}
