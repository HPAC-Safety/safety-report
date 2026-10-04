#!/usr/bin/env sh
#
# Markdown: wakes on any staged .md or .mdc file, and checks only those; the
# instruction-file check also wakes on any staged skills/ or agents/ path.
# Backstop: CI's "docs" job, which re-checks the whole tree — but a rule that
# lives only in CI is not a rule (ADR-0073), which is why it is here first.
#
# - Frontmatter: every tracked .md/.mdc declares its title, description, and
#   type (ADR-0087). The runtime prompts (the Worker's, and the translation
#   prompt in locales/) are exempt because their bytes are the model payload.
# - Links: every relative link in the staged files resolves (ADR-0183).
# - The specification index, when anything under .spec/ is staged (ADR-0183).
# - Instruction files, when a skill or role agent is staged: a generic one
#   names nothing specific to this repository, and none references a record
#   (CONV-009).
# - ADR numbers, when a decision record is staged (ADR-0091, lesson 0003).
# - Record shape, when an ADR, lesson, or convention is staged (ADR-0192).

set -u

STAGED_MARKDOWN=$(printf '%s\n' "$STAGED" | grep -E '\.(md|mdc)$') || STAGED_MARKDOWN=
STAGED_INSTRUCTIONS=$(printf '%s\n' "$STAGED" | grep -E '^(skills|agents)/') || STAGED_INSTRUCTIONS=
[ -n "$STAGED_MARKDOWN" ] || [ -n "$STAGED_INSTRUCTIONS" ] || exit 0

if ! command -v node >/dev/null 2>&1; then
	echo "pre-commit: node is not on PATH, cannot verify markdown frontmatter — see README.md" >&2
	exit 1
fi

status=0

if [ -n "$STAGED_MARKDOWN" ]; then
	# shellcheck disable=SC2086 # word-split on purpose: one path per argument
	node tools/docs/check-frontmatter.ts $STAGED_MARKDOWN || status=1

	# shellcheck disable=SC2086 # word-split on purpose: one path per argument
	node tools/docs/check-links.ts $STAGED_MARKDOWN || status=1
fi

# The specification index is generated from .spec/ (ADR-0183).
if echo "$STAGED_MARKDOWN" | grep -q '^\.spec/'; then
	node tools/spec/generate-spec-index.ts --check || status=1
fi

# A generic skill or role agent names nothing specific to this repository, so
# it can be reused elsewhere, and no instruction file references a record
# (CONV-009). Any staged skills/ or agents/ path counts, not only markdown.
if [ -n "$STAGED_INSTRUCTIONS" ]; then
	node tools/docs/check-generic-instructions.ts || status=1
fi

# A decision record's number is claimed by naming a file, and two branches
# claim the same one often enough that it is worth catching here rather than
# after a rebase (ADR-0091, lesson 0003).
if echo "$STAGED_MARKDOWN" | grep -q '^\.spec/decisions/ADR-'; then
	node tools/spec/adr-numbers.ts || status=1
fi

# Every ADR, lesson, and convention keeps its shape (ADR-0192).
if echo "$STAGED_MARKDOWN" | grep -qE '^\.spec/(decisions|lessons|conventions)/'; then
	node tools/spec/check-records.ts || status=1
fi

exit "$status"
