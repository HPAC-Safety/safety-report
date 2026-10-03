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
//   node tools/infra/check-terraform-outputs.ts
//
// The exit code is the contract.
import { globSync, readFileSync } from 'node:fs'

export const OUTPUTS_FILE = 'infra/outputs.tf'
export const WORKFLOWS_GLOB = '.github/workflows/*.yml'
// The deploy steps' scripts read outputs too (ADR-0189 moved them out of the YAML).
export const SCRIPTS_GLOB = 'tools/infra/deploy/*.ts'

/** Index of the character matching the `{` at `openIndex`, scanning forward. */
function matchBrace(text: string, openIndex: number): number {
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
function shallowKeys(text: string): Set<string> {
	const keys = new Set<string>()
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
 * map of name to a set of keys read from its `value = { ... }` object
 * literal (empty when `value` is not an object literal - a string, a list,
 * or a `concat(...)` expression nobody reads a sub-key of by name here).
 *
 * Handles one level of `for X in ... : X => { KEY = ... }` comprehension
 * (site_urls' shape: a map keyed by hostname, each entry the same nested
 * object) by reading the KEYs from the comprehension's own object literal
 * instead of the outer map's - which has no static keys, only whatever
 * hostnames apply produces. A `for` two levels deep, or one producing
 * something other than an object literal, is still out of this parser's
 * scope: it is skipped in `shallowKeys` the same as `concat(...)` is.
 */
export function parseOutputs(hcl: string): Map<string, Set<string>> {
	const outputs = new Map<string, Set<string>>()
	const outputRe = /^output\s+"([a-zA-Z0-9_]+)"\s*{/gm
	for (let match = outputRe.exec(hcl); match !== null; match = outputRe.exec(hcl)) {
		const openIndex = hcl.indexOf('{', match.index)
		const closeIndex = matchBrace(hcl, openIndex)
		const body = hcl.slice(openIndex + 1, closeIndex)

		const valueMatch = /value\s*=\s*{/.exec(body)
		let keys = new Set<string>()
		if (valueMatch) {
			const valueOpen = valueMatch.index + valueMatch[0].length - 1
			const valueClose = matchBrace(body, valueOpen)
			const valueText = body.slice(valueOpen + 1, valueClose)

			const comprehensionMatch = /=>\s*{/.exec(valueText)
			if (comprehensionMatch) {
				const innerOpen = comprehensionMatch.index + comprehensionMatch[0].length - 1
				const innerClose = matchBrace(valueText, innerOpen)
				keys = shallowKeys(valueText.slice(innerOpen + 1, innerClose))
			} else {
				keys = shallowKeys(valueText)
			}
		}
		outputs.set(match[1], keys)
	}
	return outputs
}

/**
 * `terraform output -raw NAME` / `-json NAME` references, top-level names only:
 * on a shell command line in a workflow, or as the argument list of a script's
 * `exec('terraform', ['-chdir=infra', 'output', '-raw', 'NAME'])`.
 */
export function referencedOutputNames(workflowText: string): Set<string> {
	const shell = workflowText.matchAll(/terraform\s+-chdir=\S+\s+output\s+(?:-raw|-json)\s+([A-Za-z0-9_]+)/g)
	const script = workflowText.matchAll(/'output',\s*'(?:-raw|-json)',\s*'([A-Za-z0-9_]+)'/g)
	return new Set([...shell, ...script].map((m) => m[1]))
}

/**
 * `steps.tf.outputs.KEY` references - this repository's one convention for
 * fanning `deploy_variables` into step outputs (deploy-environment.yml's
 * "Read Terraform's deploy variables" step, `id: tf`). Assumes that id; a
 * differently-named step reading deploy_variables needs its id added here.
 */
export function referencedDeployVariableKeys(workflowText: string): Set<string> {
	return new Set([...workflowText.matchAll(/steps\.tf\.outputs\.([A-Za-z0-9_]+)/g)].map((m) => m[1]))
}

/** `jq -r '.KEY'` read against a `site_urls` output, wherever it appears in the text. */
export function referencedSiteUrlsKeys(workflowText: string): Set<string> {
	const keys = new Set<string>()
	// Scoped per LINE, not per file: this repository's one convention pipes
	// `output -json site_urls` straight into `jq` on the same shell line
	// (`... output -json site_urls | jq -r '...KEY'`), and scoping any wider
	// would also catch an unrelated jq read against a different output
	// earlier or later in the same step or file.
	for (const line of workflowText.split('\n')) {
		if (!/output\s+-json\s+site_urls/.test(line)) continue
		// The trailing `.KEY` before the closing quote - matches a plain
		// '.KEY' filter and one preceded by other jq (e.g. `[.[]][0].KEY`,
		// picking the first of however many hostnames site_urls is keyed by).
		for (const found of line.matchAll(/jq\s+-r\s+'[^']*\.([A-Za-z0-9_]+)'/g)) keys.add(found[1])
	}
	return keys
}

/** What is wrong, across every workflow file, or an empty list. */
export interface Workflow {
	path: string
	text: string
}

export interface CheckInput {
	outputsHcl: string
	workflows: readonly Workflow[]
}

export function problems({ outputsHcl, workflows }: CheckInput): string[] {
	const outputs = parseOutputs(outputsHcl)
	const problems: string[] = []

	for (const { path, text } of workflows) {
		for (const name of referencedOutputNames(text)) {
			if (!outputs.has(name)) {
				problems.push(`${path} reads \`terraform output ${name}\`, which ${OUTPUTS_FILE} does not declare.`)
			}
		}

		const deployVariables = outputs.get('deploy_variables')
		if (deployVariables) {
			const keys = deployVariables
			for (const key of referencedDeployVariableKeys(text)) {
				if (!keys.has(key)) {
					problems.push(
						`${path} reads \`steps.tf.outputs.${key}\`, which is not a key of ${OUTPUTS_FILE}'s \`deploy_variables\` output.`,
					)
				}
			}
		}

		const siteUrls = outputs.get('site_urls')
		if (siteUrls) {
			const keys = siteUrls
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
export function main({ outputsHcl, workflows }: CheckInput): number {
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

const argv: (string | undefined)[] = process.argv
const scriptPath = argv[1]
const runAsCommand = (scriptPath ?? 'undefined').endsWith('/check-terraform-outputs.ts')
if (runAsCommand) {
	const outputsHcl = readFileSync(OUTPUTS_FILE, 'utf8')
	const workflows = [...globSync(WORKFLOWS_GLOB), ...globSync(SCRIPTS_GLOB)].map((path) => ({ path, text: readFileSync(path, 'utf8') }))
	process.exit(main({ outputsHcl, workflows }))
}
