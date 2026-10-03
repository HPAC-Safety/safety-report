// A JSON Schema validator for the keywords .spec/claims.schema.json uses, and
// nothing else (ADR-0193).
//
// Dependency-free, like every other tool in this directory: traceability.yml
// runs the base branch's generator with no npm install. A schema using a
// keyword this does not implement fails rather than passing unchecked, so
// teach it the keyword first.

/** The subset of JSON Schema this validates. */
export interface Schema {
	$schema?: string
	$id?: string
	$defs?: Record<string, Schema>
	$ref?: string
	title?: string
	description?: string
	type?: 'object' | 'array' | 'string' | 'integer' | 'boolean' | 'null' | ('object' | 'array' | 'string' | 'integer' | 'boolean' | 'null')[]
	properties?: Record<string, Schema>
	required?: string[]
	additionalProperties?: boolean
	items?: Schema
	uniqueItems?: boolean
	enum?: unknown[]
	pattern?: string
	minimum?: number
	minLength?: number
}

const KNOWN = new Set(['$schema', '$id', '$defs', '$ref', 'title', 'description', 'type', 'properties', 'required', 'additionalProperties', 'items', 'uniqueItems', 'enum', 'pattern', 'minimum', 'minLength'])

const typeOf = (value: unknown): string => {
	if (value === null) return 'null'
	if (Array.isArray(value)) return 'array'
	if (typeof value === 'number') return Number.isInteger(value) ? 'integer' : 'number'
	return typeof value
}

/** Every way `value` breaks `schema`, as `<path>: <problem>`; empty when it conforms. */
export function validate(value: unknown, schema: Schema, root: Schema = schema, path = '$'): string[] {
	const unknown = Object.keys(schema).filter((key) => !KNOWN.has(key))
	if (unknown.length > 0) return [`${path}: the schema uses ${unknown.join(', ')}, which tools/spec/json-schema.ts does not implement`]

	if (schema.$ref) {
		const name = /^#\/\$defs\/(.+)$/.exec(schema.$ref)?.[1]
		const target = name ? root.$defs?.[name] : undefined
		return target ? validate(value, target, root, path) : [`${path}: unresolved $ref ${schema.$ref}`]
	}

	const problems: string[] = []
	const actual = typeOf(value)
	if (schema.type) {
		const allowed = Array.isArray(schema.type) ? schema.type : [schema.type]
		if (!allowed.includes(actual as never)) return [`${path}: expected ${allowed.join(' or ')}, got ${actual}`]
	}
	if (schema.enum && !schema.enum.some((option) => JSON.stringify(option) === JSON.stringify(value))) problems.push(`${path}: ${JSON.stringify(value)} is not one of ${JSON.stringify(schema.enum)}`)

	if (typeof value === 'string') {
		if (schema.pattern && !new RegExp(schema.pattern, 'u').test(value)) problems.push(`${path}: "${value}" does not match /${schema.pattern}/`)
		if (schema.minLength !== undefined && value.length < schema.minLength) problems.push(`${path}: shorter than ${schema.minLength}`)
	}
	if (typeof value === 'number' && schema.minimum !== undefined && value < schema.minimum) problems.push(`${path}: below ${schema.minimum}`)

	if (Array.isArray(value)) {
		if (schema.items) value.forEach((item, index) => problems.push(...validate(item, schema.items as Schema, root, `${path}[${index}]`)))
		if (schema.uniqueItems && new Set(value.map((item) => JSON.stringify(item))).size !== value.length) problems.push(`${path}: items are not unique`)
	}

	if (actual === 'object') {
		const object = value as Record<string, unknown>
		for (const key of schema.required ?? []) if (!(key in object)) problems.push(`${path}: missing "${key}"`)
		for (const [key, child] of Object.entries(object)) {
			const property = schema.properties?.[key]
			if (property) problems.push(...validate(child, property, root, `${path}.${key}`))
			else if (schema.additionalProperties === false) problems.push(`${path}: unexpected "${key}"`)
		}
	}

	return problems
}
