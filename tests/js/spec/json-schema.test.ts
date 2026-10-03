import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { type Schema, validate } from '../../../tools/spec/json-schema.ts'

describe('validate', () => {
	const schema: Schema = {
		type: 'object',
		required: ['id', 'tags'],
		additionalProperties: false,
		properties: {
			id: { $ref: '#/$defs/id' },
			tags: { type: 'array', uniqueItems: true, items: { type: 'string', minLength: 1 } },
			line: { type: 'integer', minimum: 1 },
			rule: { type: ['string', 'null'] },
			engine: { enum: ['Reqnroll', 'playwright-bdd'] },
			done: { type: 'boolean' },
		},
		$defs: { id: { type: 'string', pattern: '^REQ-[A-Z]+-\\d{3}$' } },
	}

	it('accepts a conforming value', () => {
		assert.deepEqual(validate({ id: 'REQ-MED-001', tags: ['@ui'], line: 3, rule: null, engine: 'Reqnroll', done: true }, schema), [])
	})

	it('names every way a value breaks it, with the path to each', () => {
		const problems = validate({ id: 'REQ-1', tags: ['', ''], line: 0.5, rule: 3, engine: 'Cypress', extra: 1 }, schema)

		assert.deepEqual(problems, [
			'$.id: "REQ-1" does not match /^REQ-[A-Z]+-\\d{3}$/',
			'$.tags[0]: shorter than 1',
			'$.tags[1]: shorter than 1',
			'$.tags: items are not unique',
			'$.line: expected integer, got number',
			'$.rule: expected string or null, got integer',
			'$.engine: "Cypress" is not one of ["Reqnroll","playwright-bdd"]',
			'$: unexpected "extra"',
		])
		assert.deepEqual(validate({ tags: [], line: 0 }, schema), ['$: missing "id"', '$.line: below 1'])
		assert.deepEqual(validate([], schema), ['$: expected object, got array'])
	})

	it('refuses a keyword it does not implement, and a $ref it cannot resolve', () => {
		assert.match(validate('x', { oneOf: [] } as Schema)[0], /uses oneOf, which tools\/spec\/json-schema\.ts does not implement/)
		assert.deepEqual(validate('x', { $ref: '#/$defs/missing' }), ['$: unresolved $ref #/$defs/missing'])
	})
})
