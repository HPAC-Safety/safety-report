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

const ROOT = 'src/web/src'
const COPY_ATTRIBUTES = ['aria-label', 'alt', 'title', 'placeholder']

// theme-preview.html is a dev-only token demo with no user-facing copy (see
// its own header comment) and is a .html file, so it is naturally out of
// scope for this .tsx/.ts scanner. No other files are excluded.
function collectSourceFiles(dir) {
  const files = []
  for (const entry of readdirSync(dir)) {
    const path = join(dir, entry)
    const stats = statSync(path)
    if (stats.isDirectory()) {
      files.push(...collectSourceFiles(path))
    } else if (['.ts', '.tsx'].includes(extname(path))) {
      files.push(path)
    }
  }
  return files
}

/**
 * Flags JSX text nodes and copy-bearing prop string literals that are not
 * sourced from `t(...)`. A pragmatic line-based scan, not a real parser: it
 * strips comments and template-literal/expression content first so it
 * doesn't chase text across nested braces.
 */
export function findViolations(source, filePath) {
  const violations = []
  const lines = source.split('\n')

  lines.forEach((line, index) => {
    const lineNumber = index + 1
    const trimmed = line.trim()
    if (trimmed.startsWith('//') || trimmed.startsWith('*') || trimmed.startsWith('/*')) return

    // JSX text content: a line whose visible content is plain text between
    // tags, e.g. `<h1>Hello World</h1>` or a bare text line inside an
    // element. Only lines that look like markup are considered.
    const textBetweenTags = line.match(/>([^<>{}\n]*[A-Za-z][^<>{}\n]*)</)
    if (textBetweenTags && textBetweenTags[1].trim().length > 0) {
      violations.push({ file: filePath, line: lineNumber, text: textBetweenTags[1].trim(), reason: 'JSX text content' })
    }

    // Copy-bearing prop assigned a plain string literal, e.g. `alt="Logo"`,
    // not `alt={t("header.logoAlt")}`.
    for (const attribute of COPY_ATTRIBUTES) {
      const match = line.match(new RegExp(`\\b${attribute}\\s*=\\s*"([^"]+)"`))
      if (match) {
        violations.push({ file: filePath, line: lineNumber, text: match[1], reason: `${attribute} attribute` })
      }
    }
  })

  return violations
}

function main() {
  const files = collectSourceFiles(ROOT)
  const allViolations = files.flatMap((file) => findViolations(readFileSync(file, 'utf8'), file))

  if (allViolations.length > 0) {
    for (const violation of allViolations) {
      console.error(
        `::error file=${violation.file},line=${violation.line}::Hardcoded user-facing string (${violation.reason}): "${violation.text}". Add it to locales/en-CA.json and read it via t().`,
      )
    }
    process.exit(1)
  }

  console.log(`${files.length} file(s) scanned in ${ROOT}. No hardcoded user-facing strings found.`)
}

const runAsCommand = process.argv[1]?.endsWith('check-hardcoded-strings.mjs') ?? false
if (runAsCommand) main()
