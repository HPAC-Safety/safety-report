import { test } from 'node:test'
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { checkTree, checkView, main, stripComments, STRICT } from '../../tools/check-component-split.mjs'

const messages = (violations) => violations.map(({ file, line, message }) => `${file}:${line}: ${message}`)

const CLEAN_VIEW = `import { useLocale } from "../i18n/useLocale"
export function FooView() {
	const { t } = useLocale()
	return <p>{t("x")}</p>
}
`

test('strict mode is off until the last pull request of the split turns it on', () => {
	assert.equal(STRICT, false)
})

test('a clean view with its sibling passes', () => {
	const files = new Map([
		['src/web/src/Foo.tsx', 'export function Foo() { return null }'],
		['src/web/src/Foo.view.tsx', CLEAN_VIEW],
	])
	assert.deepEqual(checkTree(files), [])
})

test('a view with no sibling logic file fails at line 1', () => {
	const files = new Map([['src/web/src/Foo.view.tsx', CLEAN_VIEW]])
	assert.deepEqual(messages(checkTree(files)), ['src/web/src/Foo.view.tsx:1: a view has a sibling Foo.tsx holding its logic; none exists'])
})

test('a view may call useLocale but no other hook, and the line is named', () => {
	const source = `${CLEAN_VIEW}const [open, setOpen] = useState(false)\nconst r = useRef<HTMLDivElement>(null)\n`
	const found = checkView('V.view.tsx', source)
	assert.deepEqual(
		found.map(({ line, message }) => [line, message.match(/found (\w+)\(/)[1]]),
		[
			[6, 'useState'],
			[7, 'useRef'],
		],
	)
})

test('a word that only starts with use is not a hook call', () => {
	assert.deepEqual(checkView('V.view.tsx', 'const a = used(1)\nconst b = user(2)\nconst c = useless\n'), [])
})

test('a view importing from api/ fails, however the path is spelled', () => {
	const source = 'import { a } from "../api/client"\nimport b from "./api"\nimport c from "../apiary/x"\nconst d = await import("../../api/z")\n'
	assert.deepEqual(checkView('V.view.tsx', source).map(({ line }) => line), [1, 2, 4])
})

test('a view using storage, fetch, timers or XMLHttpRequest fails', () => {
	const source = [
		'localStorage.getItem("a")',
		'sessionStorage.clear()',
		'fetch("/x")',
		'window.setTimeout(f, 1)',
		'setInterval(f, 1)',
		'new XMLHttpRequest()',
	].join('\n')
	assert.deepEqual(checkView('V.view.tsx', source).map(({ line }) => line), [1, 2, 3, 4, 5, 6])
})

test('a prefetch name is not fetch(', () => {
	assert.deepEqual(checkView('V.view.tsx', 'prefetch(1)\nconst fetched = 1\n'), [])
})

test('a view holding module-level let or var fails, an indented one does not', () => {
	const source = 'let count = 0\nvar other = 1\nfunction F() {\n\tlet inner = 1\n}\n'
	assert.deepEqual(checkView('V.view.tsx', source).map(({ line }) => line), [1, 2])
})

test('a comment mentioning a forbidden thing does not fail, and keeps line numbers true', () => {
	const source = '// uses fetch( and localStorage\n/* useState(\n setTimeout */\nconst a = useRef(0)\n'
	const found = checkView('V.view.tsx', source)
	assert.deepEqual(found.map(({ line }) => line), [4])
	assert.equal(stripComments('a\n/* x\ny */\nb').split('\n').length, 4)
	assert.equal(stripComments('const u = "http://x"').includes('http://x'), true)
})

test('non-test code importing a test file, vitest or Testing Library fails', () => {
	const source = [
		'import { a } from "./Foo.test"',
		'import { b } from "./Foo.test.tsx"',
		'import { describe } from "vitest"',
		'import "vitest/globals"',
		'import { c } from "@vitest/spy"',
		'import { render } from "@testing-library/react"',
		'const d = await import("./x.test")',
		'import { ok } from "./latest"',
	].join('\n')
	const found = checkTree(new Map([['src/web/src/lib/a.ts', source]]))
	assert.deepEqual(found.map(({ line }) => line), [1, 2, 3, 4, 5, 6, 7])
	assert.match(found[0].message, /never part of a release/)
})

test('a test file may import vitest, Testing Library and the code under test', () => {
	const source = 'import { it } from "vitest"\nimport { render } from "@testing-library/react"\nimport { Foo } from "./Foo"\n'
	assert.deepEqual(checkTree(new Map([['src/web/src/Foo.test.tsx', source]])), [])
})

test('strict mode wants a view for every component, except main, routes and tests', () => {
	const files = new Map([
		['src/web/src/main.tsx', ''],
		['src/web/src/routes.tsx', ''],
		['src/web/src/Foo.test.tsx', ''],
		['src/web/src/helper.ts', ''],
		['src/web/src/Split.tsx', ''],
		['src/web/src/Split.view.tsx', CLEAN_VIEW],
		['src/web/src/Bare.tsx', ''],
	])
	assert.deepEqual(checkTree(files), [])
	assert.deepEqual(messages(checkTree(files, { strict: true })), ['src/web/src/Bare.tsx:1: every component has a Bare.view.tsx holding its markup; none exists'])
})

test('violations are sorted by file then line', () => {
	const files = new Map([
		['src/web/src/b.view.tsx', 'useA()\nuseB()\n'],
		['src/web/src/a.view.tsx', 'useC()\n'],
	])
	const found = checkTree(files).map(({ file, line }) => `${file}:${line}`)
	assert.deepEqual(found, ['src/web/src/a.view.tsx:1', 'src/web/src/a.view.tsx:1', 'src/web/src/b.view.tsx:1', 'src/web/src/b.view.tsx:1', 'src/web/src/b.view.tsx:2'])
})

function tree(files) {
	const root = mkdtempSync(join(tmpdir(), 'component-split-'))
	for (const [name, content] of Object.entries(files)) {
		const path = join(root, name)
		mkdirSync(join(path, '..'), { recursive: true })
		writeFileSync(path, content)
	}
	return root
}

function quietly(run) {
	const log = console.log
	const error = console.error
	const lines = []
	console.log = (...args) => lines.push(args.join(' '))
	console.error = (...args) => lines.push(args.join(' '))
	try {
		return { status: run(), lines }
	} finally {
		console.log = log
		console.error = error
	}
}

test('main reports a clean tree and exits 0', () => {
	const root = tree({ 'Foo.tsx': 'x', 'Foo.view.tsx': CLEAN_VIEW, 'readme.md': 'ignored' })
	try {
		const { status, lines } = quietly(() => main(root))
		assert.equal(status, 0)
		assert.match(lines[0], /1 view\(s\) and 2 file\(s\)/)
	} finally {
		rmSync(root, { recursive: true })
	}
})

test('main names file and line for each violation and exits 1', () => {
	const root = tree({ 'sub/Foo.tsx': 'x', 'sub/Foo.view.tsx': 'const a = 1\nuseState(0)\n' })
	try {
		const { status, lines } = quietly(() => main(root))
		assert.equal(status, 1)
		assert.match(lines[0], /sub\/Foo\.view\.tsx:2: a view calls no hook but useLocale, found useState\(/)
		assert.equal(lines.at(-1), 'check-component-split: 1 violation(s)')
	} finally {
		rmSync(root, { recursive: true })
	}
})

test('main honours strict mode passed in code', () => {
	const root = tree({ 'Bare.tsx': 'x' })
	try {
		assert.equal(quietly(() => main(root)).status, 0)
		assert.equal(quietly(() => main(root, { strict: true })).status, 1)
	} finally {
		rmSync(root, { recursive: true })
	}
})
