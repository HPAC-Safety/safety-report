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

## Workflow

1. Find or open a focused issue.
2. Branch from current `main` using `issue-<number>/<short-description>`.
3. Implement the smallest change that satisfies `/spec` and the issue.
4. Run the checks relevant to the changed surface.
5. Open a pull request with a squash-ready title and `Closes #<number>` on its
   own line in the body.
6. Address review and CI until every required check is green; squash merge only.

Do not add `Co-Authored-By` trailers or a `CODEOWNERS` file. See
[`skills/deliver-hpac-change/SKILL.md`](skills/deliver-hpac-change/SKILL.md) for
the repository delivery contract.

## Coding and test rules

- Prefer direct code. Add a port only for a real external boundary or a proven
  second implementation.
- Use Shouldly, not `Xunit.Assert` or another assertion library.
- Name .NET tests `Given_..._When_..._Then_...` and mark those sections.
- Use Mermaid for diagrams.
- Put user-facing UI text in the locale catalogues and keep English/French keys
  in parity. Database question text is manually authored in both languages.
- Never hand-edit generated files. Generated paths and commands are listed in
  [`docs/agent-workflow.md`](docs/agent-workflow.md).

When a change touches reports, questions, model input/output, attachments,
authentication, logging, deletion, review, or publication, add a focused privacy
or boundary test. Use only synthetic identities, locations, files, and report
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
