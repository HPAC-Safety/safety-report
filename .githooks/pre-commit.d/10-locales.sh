#!/usr/bin/env sh
#
# Locales: locales/en-CA.json and locales/fr-CA.json parity — a missing or
# extra key, an English `#` stub, stale fr-CA.meta.json provenance, or a
# glossary mismatch. Wakes on any staged file under locales/. Backstop: CI's
# "i18n" job, which runs the same two checks.
#
# Before checking, tools/i18n/stub-missing-translations.ts (ADR-0054) fills a
# key added to one language and not the other into the missing side with a
# `#`-prefixed copy of the text it has, both directions, and re-stages the
# result, so a bare "add a key" commit never blocks on parity by itself — only
# on a stub CI still has to resolve. It runs first, so the parity check reads
# the re-staged files.
#
# Two exceptions, on a branch only, both reported and allowed through: a
# FRENCH value still carrying its local `#` stub, and an ENGLISH value edited
# after it was translated (a stale fr-CA.meta.json source_hash). Both are
# pending work a workflow resolves: translate-locale.ts --generate queues a
# stale hash for re-translation exactly as it queues a missing key, and
# i18n-translate.yml commits the French straight onto a same-repo pull
# request's branch (ADR-0057). With no translation credential (GEMINI_API_KEY)
# locally, a developer could do nothing to satisfy a stricter rule but
# `--no-verify`, which would skip every other check too. On main the stub is
# still fatal, and CI never passes --allow-pending-translation, so one still
# cannot reach main. An ENGLISH stub is fatal everywhere — no workflow ever
# writes English — so a key added in French only needs its real English
# written by hand.

set -u

printf '%s\n' "$STAGED" | grep -q '^locales/' || exit 0

if ! command -v node >/dev/null 2>&1; then
	echo "pre-commit: node is not on PATH, cannot verify locales/ — see README.md" >&2
	exit 1
fi

status=0

if [ "$BRANCH" = "main" ]; then
	pending_translation_flag=""
else
	pending_translation_flag="--allow-pending-translation"
fi

node tools/i18n/stub-missing-translations.ts --locales locales

# fr-CA.json is generated and never hand-authored (ADR-0021, ADR-0056), so
# staging it is safe — and it is what makes the commit self-consistent: a new
# English key and its French placeholder land together rather than one
# without the other.
[ -f locales/fr-CA.json ] && git add -- locales/fr-CA.json

# en-CA.json is hand-authored, so it is re-staged only if it was already
# staged. The stubber rewrites a whole file from the working tree, so adding
# it unconditionally would sweep in unstaged edits the developer deliberately
# left out of this commit — the same trap 90-dotnet-format.sh avoids by
# re-adding only what it formatted. $STAGED was read before the stubber ran.
#
# The stubber touches en-CA.json only for a key that exists in French and not
# English, and the `#` stub that produces is fatal everywhere: no workflow
# writes English, so that one is always fixed by hand.
if printf '%s\n' "$STAGED" | grep -q '^locales/en-CA\.json$'; then
	git add -- locales/en-CA.json
fi

node tools/i18n/check-locales.ts || status=1

# shellcheck disable=SC2086 # empty on main, one flag elsewhere
node tools/i18n/translate-locale.ts --check --locales locales $pending_translation_flag || status=1

exit "$status"
