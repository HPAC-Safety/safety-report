# ADR-0039 — Path-gated required checks via job-level `if:`, never `paths:`

**Status:** Accepted

## Context

`build`, `test`, `coverage`, and `e2e` in `.github/workflows/ci.yml` compile
and run the .NET/JS application and its tests. `web` builds the Tailwind
stylesheet. None of the five reads anything under `features/`, `docs/`,
`skills/`, or `locales/` — a pull request that only edits a `.feature` file,
an ADR, or a README pays their full runtime for nothing.

The obvious fix — a `paths:` filter on the workflow's `on: pull_request:`
trigger — is exactly what ADR-0011 and `terraform.yml`'s header comment
forbid, and for good reason: `build` through `e2e` are required status
checks (`docs/github-ruleset.json`). A `paths:` filter does not skip the
job; it skips the whole workflow *run*. No run means the job's check context
is never created, and a required context that never reports blocks the pull
request permanently — the exact trap `infra` was designed around.

## Decision

**Filter with a job-level `if:`, fed by an always-runs `changes` job — not a
trigger-level `paths:` filter.**

`changes` uses `dorny/paths-filter@v4` to classify the diff into `dotnet`
(`src/**`, `tests/**`, `global.json`, `HpacSafety.slnx`,
`Directory.Build.props`, `Directory.Packages.props`, `coverlet.runsettings`,
`init-dev.sh`, the workflow file itself) and `web` (`src/web/**`,
`tools/build-css.sh`, `tools/tailwind.pin`, the workflow file). It carries no
`paths:` filter of its own and always runs. `build`, `test`, `coverage`, and
`e2e` gain `needs: changes` and `if: needs.changes.outputs.dotnet == 'true'`;
`web` gains the same with `outputs.web`.

This differs from the forbidden pattern in kind, not degree. The workflow's
trigger is unmodified, so every pull request still starts a run and still
schedules every job — `changes` included. A job whose `if:` evaluates false
completes with conclusion `skipped`, and GitHub treats a skipped required
check as passing, the same way it treats `apply` skipping in `terraform.yml`
when there is no AWS account to plan against. The context always reports;
only the work behind it is conditional.

`cucumber`, `agent-config`, and `i18n` are untouched: none of them are
"only src/ and tests/" — `cucumber` reads `features/**`, `agent-config` reads
`AGENTS.md`/`skills/`/`Skillfile`, and `i18n` reads `locales/`/`tools/`. Each
already runs proportionally to what it actually depends on.

## Alternatives considered

**A `paths:` filter on the trigger.** Rejected outright — this is the ADR-0011
trap, applied to five checks instead of one.

**Skip individual steps instead of the whole job**, matching the literal
mechanics ADR-0011's four skip branches use (job always runs; a step inside
it detects "nothing to verify" and exits 0 with a `::notice::`). Considered
for consistency, but those four branches exist because the *artifact being
verified doesn't exist yet* — a temporary, self-eliminating condition
("Superseded when: all four skip branches are gone"). A path-gated skip is
permanent and structural, not a placeholder for unwritten work, so wrapping
five jobs' worth of steps in per-step conditionals for no behavioral
difference was more code for the same outcome. A whole-job `if:` says the
same thing in one line per job and reads as "skipped" in the UI, which is a
more honest signal than a green check with a notice buried in the log.

**One shared filter for everything, including `web`.** Rejected: `web`
doesn't depend on `src/api`, `src/HpacSafety.Core`, or `tests/`, and folding
it into the `dotnet` filter would re-run a stylesheet build on pull requests
that only touch backend code, defeating the purpose.

## Consequences

- A pull request touching only `features/`, `docs/`, `skills/`, or `locales/`
  shows `build`, `test`, `coverage`, and `e2e` (and `web`, unless it also
  touched `src/web`) as skipped rather than run, and merges once the checks
  that do apply are green.
- Editing `.github/workflows/ci.yml` itself always re-runs everything it
  gates — the filter includes its own file — so a change to this gating logic
  is verified against a real run, not skipped by the same logic it changed.
- `dorny/paths-filter` is a new third-party action dependency. It is
  maintained, widely used, and Renovate already watches every pinned action
  in this repository.
- The `dotnet` filter list is a second place (besides the actual project
  files) that knows what feeds a .NET build. A new root-level file that
  affects compilation (another `Directory.*.props`, a new solution file) must
  be added here too, or `build`/`test`/`coverage`/`e2e` will silently skip
  when it changes. There is no automated check for this; it relies on the
  same discipline as the rest of `ci.yml`.
