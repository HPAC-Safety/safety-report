import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { blockReason, main } from '../../tools/guard-pr-merge.mjs'

describe('blockReason', () => {
	it('blocks a plain gh pr merge', () => {
		assert.match(blockReason('Bash', 'gh pr merge 123 --auto --squash'), /Only the owner enables auto-merge/)
	})

	it('blocks gh pr merge whatever its flags or casing', () => {
		assert.ok(blockReason('Bash', 'GH PR MERGE --squash 456'))
		assert.ok(blockReason('Bash', 'gh   pr   merge'))
	})

	it('blocks a graphql call enabling auto-merge', () => {
		const reason = blockReason('Bash', 'gh api graphql -f query=\'mutation { enablePullRequestAutoMerge(input: {}) { clientMutationId } }\'')

		assert.match(reason, /enablePullRequestAutoMerge/)
	})

	it('blocks a graphql call enqueuing a pull request', () => {
		const reason = blockReason('Bash', 'gh api graphql -f query="mutation { enqueuePullRequest(input: {}) { clientMutationId } }"')

		assert.match(reason, /enqueuePullRequest/)
	})

	it('blocks the mutation name even without a literal "gh api graphql" call, so rewrapping cannot evade it', () => {
		assert.ok(blockReason('Bash', 'curl -X POST -d \'{"query":"mutation{enablePullRequestAutoMerge(input:{})}"}\' https://api.github.com/graphql'))
	})

	it('allows an unrelated gh pr command', () => {
		assert.equal(blockReason('Bash', 'gh pr view 123 --json state'), null)
		assert.equal(blockReason('Bash', 'gh pr create --title "x" --body "y"'), null)
	})

	it('allows an unrelated graphql call', () => {
		assert.equal(blockReason('Bash', 'gh api graphql -f query="query { viewer { login } }"'), null)
	})

	it('allows a non-Bash tool call', () => {
		assert.equal(blockReason('Read', 'gh pr merge 1'), null)
	})

	it('allows a missing or empty command', () => {
		assert.equal(blockReason('Bash', undefined), null)
		assert.equal(blockReason('Bash', ''), null)
		assert.equal(blockReason('Bash', '   '), null)
	})
})

describe('main', () => {
	function runMain(payload) {
		const errors = []
		const original = console.error
		console.error = (...args) => errors.push(args.join(' '))
		try {
			return { code: main(JSON.stringify(payload)), errors }
		} finally {
			console.error = original
		}
	}

	it('exits 2 and explains, for gh pr merge', () => {
		const { code, errors } = runMain({ tool_name: 'Bash', tool_input: { command: 'gh pr merge 99 --auto --squash' } })

		assert.equal(code, 2)
		assert.match(errors.join('\n'), /Only the owner enables auto-merge/)
	})

	it('exits 0 for an allowed Bash command', () => {
		assert.equal(runMain({ tool_name: 'Bash', tool_input: { command: 'gh pr view 99' } }).code, 0)
	})

	it('exits 0 for a payload it does not recognise, rather than blocking', () => {
		assert.equal(main('not json'), 0)
		assert.equal(runMain({ tool_name: 'Edit', tool_input: { file_path: 'x' } }).code, 0)
	})
})
