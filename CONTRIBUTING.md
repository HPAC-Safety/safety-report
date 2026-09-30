---
title: Contributing
description: How to set up the repository, branch, verify a change, and open a pull request.
type: guide
---

# Contributing

Read [`AGENTS.md`](AGENTS.md) and the canonical
[`features/README.md`](features/README.md) before changing product behavior. The
repository processes real accident reports, so privacy and publication rules
are part of correctness.

## Setup

```bash
git clone git@github.com:HPAC-Safety/safety-report.git
cd safety-report
./init-dev.sh
```

Use `./init-dev.sh --check` to inspect prerequisites without installing. Windows
contributors run the script from Git Bash.

`./init-dev.sh --obsidian` additionally renders the graphify knowledge graph
into `obsidian-vault/` — notes plus a canvas, openable as an Obsidian vault.
It is opt-in, clone-local, gitignored, and rebuilt from the graph on each run
with the flag; nothing else in the repository reads it.

`.claude/settings.json` is tracked and team-shared: a few PATH-based Claude
Code hooks (the pull-request merge/enqueue guard, ADR-0147; graphify's search
and read reminders where `graphify` is on `PATH`), nothing machine- or
person-specific. `init-dev.sh` never writes to it — in particular, it skips
`graphify claude install`, which would otherwise overwrite it with a hook
hardcoding your own machine's graphify binary path. Keep personal hooks or
settings in `.claude/settings.local.json` instead, which stays untracked, or
in `~/.claude/settings.json` for every project.

## Workflow

1. Find or open a focused issue.
2. Branch from current `main` using `issue-<number>/<short-description>`.
3. Implement the smallest change that satisfies `/features` and the issue.
4. Run the tests for the code you changed, natively (a filtered `dotnet test`,
   and `CI=1 npm test` for a touched e2e spec), then
   `tools/ci-local.sh --body <pr-body.md>`: it runs the pull request's fast
   checks under act (body checks, `feature-coverage`, and the cheap `ci.yml`
   jobs), so a failure is caught before the PR rather than in it. GitHub CI,
   the coverage ratchet included, is the full gate; `--full` runs all of it
   locally (see the README).
5. Open a pull request with a squash-ready title and `Closes #<number>` on its
   own line in the body.
6. Address review and CI until every required check is green; squash merge only.

Do not add `Co-Authored-By` trailers or a `CODEOWNERS` file. See
[`skills/deliver-change/SKILL.md`](skills/deliver-change/SKILL.md) and
[`skills/deliver-hpac-change/SKILL.md`](skills/deliver-hpac-change/SKILL.md) for
the repository delivery contract.

## Coding and test rules

- Prefer direct code. Add a port only for a real external boundary or a proven
  second implementation.
- Use Shouldly, not `Xunit.Assert` or another assertion library.
- Name .NET tests `GivenX_WhenY_ThenZ` — three PascalCase segments joined by
  single underscores, no articles
  ([ADR-0069](docs/decisions/ADR-0069-scannable-given-when-then-test-names.md))
  — and mark those sections in the body.
- Use Mermaid for diagrams.
- Put user-facing UI text in the locale catalogues and keep English/French keys
  in parity. Database question text is manually authored in both languages.
- Open every markdown file with YAML frontmatter naming its `title`,
  `description`, and `type`
  ([ADR-0087](docs/decisions/ADR-0087-every-markdown-file-declares-itself.md)).
  `node tools/check-frontmatter.mjs` checks the tree; the pre-commit hook
  checks what you staged.
- Never hand-edit generated files. Generated paths and commands are listed in
  [`docs/agent-workflow.md`](docs/agent-workflow.md).

When a change touches reports, questions, model input/output, attachments,
authentication, authorization, logging, deletion, review, or publication, add a
focused privacy or boundary test. Use only synthetic identities, locations, files, and report
content. Runtime prompt changes create a new prompt version; they do not add a
second model stage.

## Dependencies

NuGet versions belong in `Directory.Packages.props`; project
`PackageReference` items do not carry versions. Keep package groups and entries
sorted. Renovate handles scheduled updates, while major changes receive manual
review.

## Security

Do not report vulnerabilities in a public issue. Follow
[`SECURITY.md`](SECURITY.md).
