import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { anchors, links, slug, splitTarget } from '../../tools/markdown-links.mjs'

const targets = (text, options) => links(text, options).map((link) => link.target)

describe('links', () => {
	it('finds inline links, images, and reference definitions', () => {
		const text = 'See [a](a.md) and ![b](img/b.png "B").\n\n[c]: ../c.md'

		assert.deepEqual(targets(text), ['a.md', 'img/b.png', '../c.md'])
	})

	it('keeps a link whose text is a code span', () => {
		assert.deepEqual(targets('[`.spec/features/README.md`](.spec/features/README.md)'), ['.spec/features/README.md'])
	})

	it('ignores a link inside a code span, a fence, or a comment', () => {
		const text = ['`[a](a.md)`', '```md', '[b](b.md)', '```', '<!-- [c](c.md) -->', '~~~', '[d](d.md)', '~~~'].join('\n')

		assert.deepEqual(targets(text), [])
	})

	it('reports the line and the column of the target, so a rewrite can replace it in place', () => {
		const [link] = links('first\nsee [x](../x.md)')

		assert.equal(link.line, 2)
		assert.equal('see [x](../x.md)'.slice(link.column, link.column + link.target.length), '../x.md')
	})

	it('reads an href only when asked', () => {
		const text = '/// <see href="../docs/a.md">A</see>'

		assert.deepEqual(targets(text), [])
		assert.deepEqual(targets(text, { html: true }), ['../docs/a.md'])
	})

	it('keeps balanced parentheses in a target', () => {
		assert.deepEqual(targets('[w](https://en.wikipedia.org/wiki/A_(b))'), ['https://en.wikipedia.org/wiki/A_(b)'])
	})
})

describe('splitTarget', () => {
	it('separates the path, query, and anchor', () => {
		assert.deepEqual(splitTarget('../a.md?x=1#part'), { raw: '../a.md?x=1#part', path: '../a.md', anchor: 'part', query: 'x=1', external: false, trailingSlash: false })
	})

	it('marks a URL with a scheme or a protocol-relative host as external', () => {
		assert.equal(splitTarget('https://example.com').external, true)
		assert.equal(splitTarget('mailto:a@example.com').external, true)
		assert.equal(splitTarget('//example.com/a').external, true)
		assert.equal(splitTarget('docs/').trailingSlash, true)
	})
})

describe('slug', () => {
	it('matches the anchor GitHub gives a heading', () => {
		assert.equal(slug('Verify and publish'), 'verify-and-publish')
		assert.equal(slug('The `feature-coverage` exemption'), 'the-feature-coverage-exemption')
		assert.equal(slug('Claim IDs and the matrix'), 'claim-ids-and-the-matrix')
		assert.equal(slug('[Linked](x.md) heading: yes!'), 'linked-heading-yes')
	})
})

describe('anchors', () => {
	it('numbers a repeated heading and skips headings in a fence', () => {
		const offered = anchors(['# Notes', '## Notes', '```', '# Hidden', '```', '<a id="custom"></a>'].join('\n'))

		assert.deepEqual([...offered].sort(), ['custom', 'notes', 'notes-1'])
	})
})
