#!/usr/bin/env node
// No hardcoded user-facing strings in src/web markup.
//
// The `i18n` CI job runs this with no `npm ci` step (see
// .github/workflows/ci.yml) so it has no dependency on the TypeScript
// compiler API or any other package — plain node:fs and regular expressions
// only, in the same pragmatic-grep spirit as the "No raw hex in markup"
// check in the `web` job.
//
// Coupled deliberately to the lookup function being named `t`: every
// component reads copy via `const { t } = useLocale()` (src/web/src/i18n),
// so a call site wrapped in `t(...)` is recognised as sourced from the
// locale catalogue and anything else is flagged.
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { extname, join } from 'node:path'
import { isMain } from '../lib/actions.ts'

const ROOT = 'src/web/src'
const COPY_ATTRIBUTES = ['aria-label', 'alt', 'title', 'placeholder']

// theme-preview.html is a dev-only token demo with no user-facing copy (see
// its own header comment) and is a .html file, so it is naturally out of
// scope for this .tsx/.ts scanner. Unit tests (*.test.ts, *.test.tsx) are
// excluded too: they assert on rendered copy and hold fixtures, never a
// screen a reader sees (ADR-0188).
export function collectSourceFiles(dir: string): string[] {
  const files: string[] = []
  for (const entry of readdirSync(dir)) {
    const path = join(dir, entry)
    const stats = statSync(path)
    if (stats.isDirectory()) {
      files.push(...collectSourceFiles(path))
    } else if (['.ts', '.tsx'].includes(extname(path)) && !/\.test\.tsx?$/.test(path)) {
      files.push(path)
    }
  }
  return files
}

/**
 * Blanks what is TypeScript rather than markup on one line, so its `>` and `<`
 * are not read as the ends of a JSX tag: an arrow's `=>`, and the type
 * arguments of a generic (`Promise<void>`, `useRef<T>(null)`, `<T,>(props)`).
 * A generic's `<` follows an identifier directly and opens with a name; a JSX
 * closing tag opens with `/`, so `Hello</b>` is left alone.
 */
export function blankTypePositions(line: string): string {
  return line
    .replace(/=>/g, '  ')
    .replace(/(?<=[\w$\])])<[A-Za-z_$][^<>()=]*(?:<[^<>]*>[^<>()=]*)*>/g, (match: string) => ' '.repeat(match.length))
    .replace(/<[A-Z]\w*(?:,|\s+extends\s+[^<>]*,?)>(?=\s*\()/g, (match: string) => ' '.repeat(match.length))
}

/**
 * Flags JSX text nodes and copy-bearing prop string literals that are not
 * sourced from `t(...)`. A pragmatic line-based scan, not a real parser: it
 * strips comments and template-literal/expression content first so it
 * doesn't chase text across nested braces.
 */
export interface Violation {
  file: string
  line: number
  text: string
  reason: string
}

export function findViolations(source: string, filePath: string): Violation[] {
  const violations: Violation[] = []
  const lines = source.split('\n')

  lines.forEach((line, index) => {
    const lineNumber = index + 1
    const trimmed = line.trim()
    if (trimmed.startsWith('//') || trimmed.startsWith('*') || trimmed.startsWith('/*')) return

    // JSX text content: a line whose visible content is plain text between
    // tags, e.g. `<h1>Hello World</h1>` or a bare text line inside an
    // element. Only lines that look like markup are considered.
    const textBetweenTags = blankTypePositions(line).match(/>([^<>{}\n]*[A-Za-z][^<>{}\n]*)</)
    const jsxText = textBetweenTags?.[1]?.trim()
    if (jsxText !== undefined && jsxText.length > 0) {
      violations.push({ file: filePath, line: lineNumber, text: jsxText, reason: 'JSX text content' })
    }

    // Copy-bearing prop assigned a plain string literal, e.g. `alt="Logo"`,
    // not `alt={t("header.logoAlt")}`.
    for (const attribute of COPY_ATTRIBUTES) {
      const match = line.match(new RegExp(`\\b${attribute}\\s*=\\s*"([^"]+)"`))
      const literal = match?.[1]
      if (literal !== undefined) {
        violations.push({ file: filePath, line: lineNumber, text: literal, reason: `${attribute} attribute` })
      }
    }
  })

  return violations
}

/** Scans every `.ts`/`.tsx` file under `root` and returns all violations found. */
export function scanRepository(root: string): { files: string[]; violations: Violation[] } {
  const files = collectSourceFiles(root)
  return { files, violations: files.flatMap((file) => findViolations(readFileSync(file, 'utf8'), file)) }
}

/**
 * Scans `root` and reports the result the way the command line does, without
 * exiting the process — so a test can call this directly, in-process, and
 * exercise both outcomes. Returns the exit code the command should use.
 */
export function main(root: string = ROOT): number {
  const { files, violations } = scanRepository(root)

  if (violations.length > 0) {
    for (const violation of violations) {
      console.error(
        `::error file=${violation.file},line=${violation.line}::Hardcoded user-facing string (${violation.reason}): "${violation.text}". Add it to locales/en-CA.json and read it via t().`,
      )
    }
    return 1
  }

  console.log(`${files.length} file(s) scanned in ${root}. No hardcoded user-facing strings found.`)
  return 0
}

const runAsCommand = isMain(import.meta.url)
if (runAsCommand) process.exit(main())
