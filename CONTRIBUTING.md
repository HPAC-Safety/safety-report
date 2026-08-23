# Contributing

This is a public repository for a system used by the Hang Gliding and
Paragliding Association of Canada. Contributions are welcome from anyone.

Before writing code, read [`AGENTS.md`](AGENTS.md). It applies to humans too —
the invariants there are the reason this system exists.

## Setting up

```bash
git clone git@github.com:HPAC-Safety/safety-report.git
cd safety-report
./init-dev.sh
```

`init-dev.sh` installs the .NET SDK, Docker, and Node at the versions this
repository pins, and finishes by telling you whether you are ready or what is
left to do by hand. Windows contributors run it from **Git Bash**. It is
idempotent, so re-run it whenever a build fails for a reason you cannot place —
`./init-dev.sh --check` reports without installing anything.

The full prerequisite table, the options, and the things the script deliberately
leaves to you are in the [README](README.md#getting-started). Why it is one
shell script and not PowerShell or a devcontainer:
[ADR-0015](docs/decisions/ADR-0015-one-shell-script-for-development-setup.md).

## Workflow

1. Find or open an issue. Work is filed under the **Foundation**, **Phase 1**,
   and **Phase 2** milestones.
2. Branch: `area/short-description` (e.g. `worker/outbox-backoff`).
3. Open a pull request. `main` is protected and every change needs one approving
   review from a repository administrator.
4. **Put `Closes #123` in the pull request body.** The `linked-issue` check
   fails without it. GitHub closes an issue on merge only when a closing keyword
   appears in the body or the commit message — `See #123` does nothing. Because
   the repository squashes with the body as the commit message, the body is the
   one place that reliably works.
5. **Squash merge only.** The PR title becomes the commit message, so write it
   as one.
6. No `Co-Authored-By` trailers.

## Approvals

Only accounts with **write or admin** permission can approve — GitHub does not
count approving reviews from anyone else toward the requirement. Outside
contributors are very welcome to open PRs and to comment; approval rests with
the administrators.

**There is deliberately no `CODEOWNERS` file, and none should be added.**
Approver management lives on the repository access page precisely so that
changing who can approve does not itself require a pull request. Adding
`CODEOWNERS` would move that control into a committed file and break the
workflow. See [ADR-0008](docs/decisions/ADR-0008-github-workflow.md).

Write access *is* approval power. It is not granted casually; `triage` is the
right role for someone helping with issues and labels.

## House rules

These are enforced in CI, so knowing them saves a round trip:

- **Diagrams are Mermaid.** No ASCII art, anywhere.
- **Assertions use Shouldly.** Not `Assert.*`, not FluentAssertions.
- **Tests are named `Given_..._When_..._Then_...`** with the sections marked in
  the body.
- **No hardcoded user-facing strings.** Add a key to `locales/en-CA.json` —
  including in the admin UI, error messages, and `aria-label`s.
- **SOLID, and a named Gang of Four pattern where one fits.** `Core` depends on
  nothing; vendor SDKs cross the boundary through an adapter; retry and logging
  are decorators. A pattern that abstracts a variation which does not exist is a
  layer, not a pattern — if you cannot say what varies, write the plain code.
- **Never hand-edit a generated file.** See the table in
  [`docs/agent-workflow.md`](docs/agent-workflow.md).

## If your change touches personal data

Question privacy, model input, prompts, summaries, uploads, logging, or
authentication:

- Add a privacy-contract test for the boundary you changed. For model output,
  use a controlled or recorded response, assert the synthetic identifier was
  present in input and absent from output, and assert useful non-private facts
  survived.
- Bump the prompt version if you changed prompt text — do not edit in place.
- Expect close review. The PR template asks about this explicitly; answer
  honestly, it routes reviewer attention rather than creating work.

Never paste real report content into an issue, a PR, or a test fixture.

## Working with an AI agent

This project is built primarily by AI agents and is configured to be
tool-agnostic. See [`docs/agent-workflow.md`](docs/agent-workflow.md).

`./init-dev.sh` runs `skillfile install` for you. Windows contributors need
`git config core.symlinks true` and Developer Mode, or the agent instruction
files arrive as plain text containing a path.

## Dependencies

All NuGet versions live in `Directory.Packages.props` via Central Package
Management. **A `PackageReference` in a `.csproj` never carries a `Version`
attribute.**

Packages are grouped into `ItemGroup`s labelled by family, sorted alphabetically
by label, and alphabetically by `Include` within each group. `renovate.json`
groups its update PRs on the same boundaries, so a family moves as a unit and
the diff stays readable.

Renovate runs **weekly, early Monday**. Patch and minor updates are approved by
Renovate itself and automerge once CI is green; major updates are labelled
`breaking-change` and reviewed by hand. Security advisories bypass the schedule.

The Renovate app holds write access, which is the **one deliberate exception** to
the rule above that write access is approval power. It approves only its own
dependency PRs, only patch and minor, and GitHub's auto-merge still holds them
until required checks pass. See
[ADR-0008](docs/decisions/ADR-0008-github-workflow.md).

Adding a package: add a `PackageVersion` to the right labelled group (create the
group if the family is new, keeping the file sorted), then a bare
`PackageReference` in the project that needs it.

## Security

Do not report vulnerabilities in a public issue. See
[`SECURITY.md`](SECURITY.md).
