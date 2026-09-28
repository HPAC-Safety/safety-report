#!/usr/bin/env node
// Refuses a Bash tool call that would merge or enqueue a pull request
// (ADR-0147 amendment, issue #427). Only the owner enables auto-merge or
// enqueues a pull request, by hand — an agent opens the pull request, gets
// its checks green, and reports it ready.
//
// Wired in as a Claude Code `PreToolUse` hook (matcher: Bash) directly in the
// tracked, team-wide `.claude/settings.json`: `node
// "$CLAUDE_PROJECT_DIR"/tools/guard-pr-merge.mjs`. That file carries only
// PATH-based, machine-independent hooks — no absolute path, nothing
// person-specific — which is also why `init-dev.sh` never writes to it.
//
//   <hook JSON on stdin> | node tools/guard-pr-merge.mjs
//
// Exit code is the contract Claude Code reads: 0 allows the tool call, 2
// blocks it and shows stderr to the agent as the reason. Any input that is
// not a Bash call with a command, or that matches neither rule, is allowed.
//
// The command is split into shell segments (on unquoted &&, ||, ;, |, and
// newlines) so a check can ask "what command is this segment actually
// running" rather than pattern-matching the whole string. That is what lets
// `git commit -m "...gh pr merge..."`, `gh pr view 1 | grep "gh pr merge"`,
// and `rg enablePullRequestAutoMerge` pass while a real `gh`, `curl`, or
// `wget` invocation is still caught, including one wrapped in flags, an
// alias, or a different-cased command.
import { readFileSync } from 'node:fs'

// Commands able to make the network call a bypass needs — the mutation-name
// scan (which must catch a rewrap through curl/wget, not just `gh` itself)
// only ever looks inside a segment led by one of these. Any other leading
// command (git, grep, rg, sed, cat, echo, node, python, …) is never scanned
// for the mutation name, which is what keeps a commit message or a search
// for the mutation's own name from being blocked.
const NETWORK_COMMANDS = new Set(['gh', 'curl', 'wget', 'http', 'httpie', 'xh'])

// Leading words that wrap the real command without being it.
const WRAPPER_WORDS = new Set(['sudo', 'env', 'command', 'nice', 'ionice', 'time', 'exec', 'xargs'])

/** Splits a shell command into top-level segments on &&, ||, ;, |, and newlines — never inside quotes. */
function splitSegments(command) {
	const segments = []
	let current = ''
	let quote = null // ' or " while inside one, else null

	for (let i = 0; i < command.length; i += 1) {
		const ch = command[i]
		const next = command[i + 1]

		if (quote) {
			current += ch
			if (ch === quote && command[i - 1] !== '\\') quote = null
			continue
		}

		if (ch === "'" || ch === '"') {
			quote = ch
			current += ch
			continue
		}

		if ((ch === '&' && next === '&') || (ch === '|' && next === '|')) {
			segments.push(current)
			current = ''
			i += 1
			continue
		}

		if (ch === ';' || ch === '|' || ch === '\n') {
			segments.push(current)
			current = ''
			continue
		}

		current += ch
	}

	segments.push(current)
	return segments.map((segment) => segment.trim()).filter((segment) => segment.length > 0)
}

/** Tokenizes a segment on whitespace, keeping quoted spans as one token (quotes stripped). */
function tokenize(segment) {
	const tokens = []
	let current = ''
	let quote = null

	for (let i = 0; i < segment.length; i += 1) {
		const ch = segment[i]

		if (quote) {
			if (ch === quote && segment[i - 1] !== '\\') {
				quote = null
			} else {
				current += ch
			}
			continue
		}

		if (ch === "'" || ch === '"') {
			quote = ch
			continue
		}

		if (/\s/.test(ch)) {
			if (current.length > 0) {
				tokens.push(current)
				current = ''
			}
			continue
		}

		current += ch
	}

	if (current.length > 0) tokens.push(current)
	return tokens
}

/** The command this segment actually runs — past variable assignments and wrapper words, basename only. */
function leadingCommand(segment) {
	const tokens = tokenize(segment)
	let index = 0

	while (index < tokens.length && /^[A-Za-z_][A-Za-z0-9_]*=/.test(tokens[index])) {
		index += 1
	}

	while (index < tokens.length && WRAPPER_WORDS.has(tokens[index].toLowerCase())) {
		index += 1
		// A flagged wrapper argument (e.g. `nice -n 10`) is not the command either.
		while (index < tokens.length && tokens[index].startsWith('-')) index += 1
	}

	const token = tokens[index]
	if (!token) return null

	const basename = token.split('/').pop()
	return basename ? basename.toLowerCase() : null
}

const GH_PR_MERGE_INLINE = /\bgh\s+pr\s+merge\b/i

/** True when a `gh`-led segment merges a pull request, however its tokens are ordered or spaced. */
function mergesAPullRequest(segment, tokens) {
	if (GH_PR_MERGE_INLINE.test(segment)) return true

	// `gh pr -R owner/repo merge 123`, `gh pr --repo owner/repo merge 123`: the
	// same subcommand pair, reordered around a flag with its own value.
	const lower = tokens.map((token) => token.toLowerCase())
	return lower.includes('pr') && lower.includes('merge')
}

const AUTOMERGE_MUTATION_NAMES = ['enablePullRequestAutoMerge', 'enqueuePullRequest', 'mergePullRequest']
const AUTOMERGE_MUTATIONS = new RegExp(`\\b(${AUTOMERGE_MUTATION_NAMES.join('|')})\\b`)

const GRAPHQL_EXTERNAL_QUERY = /(?:^|\s)(?:-[fF]\s*['"]?query=@\S+|--input[= ]\S+)/

const REST_MERGE_PATH = /\bpulls\/\S+\/merge\b/i
const PUT_METHOD = /(?:^|\s)(?:-X\s*PUT|-XPUT|--method[= ]PUT)\b/i

/** Decides whether a Bash command merges or enqueues a pull request. Returns the reason, or null to allow it. */
export function blockReason(toolName, command) {
	if (toolName !== 'Bash') return null
	if (typeof command !== 'string' || command.trim() === '') return null

	for (const segment of splitSegments(command)) {
		const leading = leadingCommand(segment)
		const tokens = tokenize(segment)

		if (leading === 'gh') {
			if (mergesAPullRequest(segment, tokens)) {
				return (
					'Only the owner enables auto-merge or merges a pull request, by hand ' +
					'(ADR-0147 amendment, issue #427). Open the pull request, get its ' +
					'checks green, and report it ready — do not run `gh pr merge`.'
				)
			}

			if (tokens.some((token) => token.toLowerCase() === 'alias') && tokens.some((token) => token.toLowerCase() === 'set')) {
				if (/\bpr\s+merge\b/i.test(segment) || AUTOMERGE_MUTATIONS.test(segment)) {
					return (
						'Only the owner enables auto-merge or merges a pull request, by hand ' +
						'(ADR-0147 amendment, issue #427). This aliases a command that would — ' +
						'remove it rather than giving the merge another name.'
					)
				}
			}

			if (tokens.some((token) => token.toLowerCase() === 'api')) {
				if (REST_MERGE_PATH.test(segment) && PUT_METHOD.test(segment)) {
					return (
						'Only the owner enables auto-merge or merges a pull request, by hand ' +
						'(ADR-0147 amendment, issue #427). This is a REST `PUT .../merge` call — ' +
						'remove it and report the pull request ready instead.'
					)
				}

				if (/\bgraphql\b/i.test(segment) && GRAPHQL_EXTERNAL_QUERY.test(segment)) {
					return (
						'Only the owner enables auto-merge or merges a pull request, by hand ' +
						'(ADR-0147 amendment, issue #427). This GraphQL call reads its query from ' +
						'a file this hook cannot inspect, so it is refused outright — inline the ' +
						'query, or ask the owner.'
					)
				}
			}
		}

		if (NETWORK_COMMANDS.has(leading)) {
			const mutation = segment.match(AUTOMERGE_MUTATIONS)
			if (mutation) {
				return (
					`Only the owner enables auto-merge or enqueues a pull request, by hand ` +
					`(ADR-0147 amendment, issue #427). This command names the ` +
					`\`${mutation[1]}\` mutation — remove it and report the pull request ` +
					'ready instead.'
				)
			}
		}
	}

	return null
}

/** Reads one Claude Code PreToolUse hook payload and returns the exit code. */
export function main(input) {
	let payload
	try {
		payload = JSON.parse(input)
	} catch {
		return 0 // not a hook payload we understand; never block on a parse failure
	}

	const toolName = payload?.tool_name
	const command = payload?.tool_input?.command
	const reason = blockReason(toolName, command)
	if (reason === null) return 0

	console.error(reason)
	return 2
}

const runAsCommand = String(process.argv[1]).endsWith('guard-pr-merge.mjs')
if (runAsCommand) {
	process.exit(main(readFileSync(0, 'utf8')))
}
