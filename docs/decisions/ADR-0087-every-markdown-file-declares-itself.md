---
title: Every markdown file declares what it is
description: Every tracked .md and .mdc file opens with a YAML frontmatter block.
type: adr
status: accepted
date: 2026-09-22
decision-makers: Chase Florell
keywords: frontmatter, YAML, markdown, documentation, validation, pre-commit, CI gate
---

# ADR-0087 — Every markdown file declares what it is

## Context

The repository tracks 142 markdown files. Eighty-nine carry YAML frontmatter:
the ADRs, which have carried `status`/`date`/`decision-makers`/`keywords` since
[ADR-0001](ADR-0001-repository-and-agent-configuration.md), and the twelve
project-owned skills, which carry the `name`/`description` pair Claude Code and
`skillfile` require. The other fifty-three carry nothing — every `features/`
page, every `docs/` page, every component README, and every root file.

That means a document's kind is inferred from its path. A reader, human or
agent, learns that `docs/decisions/ADR-0047-*.md` is a decision record and
`features/media/README.md` is supporting detail by knowing the repository, not
by reading the file. Nothing distinguishes a canonical specification page from
a narrative audit page sitting in the same directory, and a new file can be
born in any shape at all because no shape is defined.

[ADR-0083](ADR-0083-specification-driven-development.md) makes the
specification the artifact an agent is handed. An artifact that does not say
what it is puts that burden back on convention.

## Decision

Every tracked `.md` and `.mdc` file opens with a YAML frontmatter block.

**Core keys, required on every non-exempt file:** `title`, `description`, and
`type`, where `type` is one of `adr`, `spec`, `guide`, `readme`, `lesson`,
`instructions`, or `template`.

**Per-type keys:**

- `adr` adds `status`, `date`, `decision-makers`, and `keywords`, exactly as
  the existing records already carry them. No ADR is reformatted; the three
  core keys are added to what is there.
- `spec` adds `area`, naming the feature folder or docs page the claims of that
  area live under. When
  [ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)
  is accepted, `spec` also carries `claims`, the claim-ID prefix that page
  owns; it is not required before those prefixes exist.
- `lesson` adds `date`, `issue`, and `status`.
- `guide`, `readme`, `instructions`, and `template` are core only.

**A file an AI tool parses keeps that tool's standard and nothing else.** A
`skills/*/SKILL.md` and an `agents/*.md` require exactly `name` and
`description`, and the validator resolves their type from the path rather than
demanding a repository-specific key. Injecting `title` or `type` into a file
that Claude Code, `skillfile`, or another vendor's loader reads would be
inventing a private extension to somebody else's format for the convenience of
our own checker. Optional upstream keys — `allowed-tools`, `license`, `model`,
`metadata` — remain permitted.

**One exemption:** `src/HpacSafety.Worker/Prompts/**`. Those bytes are the
runtime model payload, and a prompt version is immutable once it has been used
for a summary. Adding frontmatter would either change what the model receives
or require the Worker to strip it, putting a parser on the privacy-sensitive
path to satisfy a documentation rule. The exemption is named in the validator
with that reason beside it.

**Enforcement is local first.** `.githooks/pre-commit` checks staged markdown,
and CI's `docs` job checks the tree. A guard that exists only where a CI
command line is typed is not a guard
([ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)).

## Consequences

- `tools/check-frontmatter.mjs` is the one authority on the shape, in the same
  dependency-free style as `tools/check-locales.mjs`; its exit code is the
  contract and its errors name the file, the key, and the expected shape.
- A new markdown file cannot be added without stating what it is.
- `docs` becomes a required status check, and the job is extended rather than
  duplicated when the traceability matrix arrives.
- Symlinked instruction files (`CLAUDE.md`, `.github/copilot-instructions.md`,
  `.cursor/rules/agents.mdc`) are skipped because they resolve to `AGENTS.md`,
  which is validated once as `type: instructions`.
- `AGENTS.md` carries the repository's own core keys rather than Cursor's
  `.mdc` rule shape (`globs`, `alwaysApply`). Those three consumers ignore keys
  they do not recognize, and one shared file cannot be shaped for one of them
  without misrepresenting itself to the other two.

## Alternatives

- **Require frontmatter only where a tool reads it.** Rejected: that is the
  status quo, and it is why a specification page and an audit page are
  indistinguishable from their first line.
- **A rich core with `status`, `date`, and `owner` on every file.** Rejected:
  dates and owners on 142 files go stale faster than anyone updates them, and a
  stale governance field is worse than no field because it is believed.
- **Give `SKILL.md` and `agents/*.md` the core keys too, for uniformity.**
  Rejected: those files are parsed by third-party loaders. Uniformity inside
  this repository is not worth adding unrecognized keys to somebody else's
  format, and the path already determines the type unambiguously.
- **Add frontmatter to the Worker prompts and strip it at load.** Rejected: see
  the exemption above. A prompt version's bytes are its identity.
- **Enforce in CI only.** Rejected on this repository's own evidence
  (ADR-0073).

## Related

- [ADR-0001](ADR-0001-repository-and-agent-configuration.md)
- [ADR-0073](ADR-0073-a-ui-scenario-is-skipped-by-reqnroll-itself.md)
- [ADR-0083](ADR-0083-specification-driven-development.md)
- [ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)
