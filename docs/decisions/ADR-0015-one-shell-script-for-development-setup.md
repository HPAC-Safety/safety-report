# ADR-0015 — One POSIX `sh` script for development environment setup

**Status:** Accepted

## Context

Until now the prerequisites for building this repository were spread across the
files that happened to pin them — `global.json`, `.github/workflows/ci.yml`,
`.config/dotnet-tools.json`, `Skillfile.lock` — and nowhere as a list. "Getting
started" in `README.md` was three lines that assumed a machine somebody had
already set up by hand.

The failure modes were all silent. A contributor who installed "the latest
.NET" got an SDK `global.json` rejects. One without Docker got a `dotnet test`
run that failed inside Testcontainers with a socket error rather than a sentence
saying Docker was missing. One with the Node their distribution ships got a
coverage gate that behaved differently from the one CI would run against their
pull request.

This repository is built largely by AI agents, which sharpens the problem: an
agent starting on a fresh container has no way to discover the prerequisites
except by reading five files and inferring, and no way to verify it succeeded
except by running a build and interpreting the wreckage.

The requirement was one script, at the repository root, named `init-dev.<ext>`,
working on Windows, macOS, and Linux, deterministic and idempotent.

## Decision

**`init-dev.sh` — a single POSIX `sh` script.** macOS and Linux run it natively.
Windows runs it under Git Bash.

Determinism comes from refusing to restate a version. The .NET SDK version is
parsed out of `global.json` at run time and the Node major out of
`.github/workflows/ci.yml`. There is no second copy of either number in the
script, so neither can drift. Whether an installed SDK satisfies `global.json`
is decided by running `dotnet --version` inside the repository, not by comparing
version strings here — `rollForward` semantics stay in the one place that
already implements them.

Idempotency comes from probing before acting, including for the two installs
that land outside `PATH` (`~/.dotnet`, `~/.local/bin`). Without that probe every
subsequent run re-invokes an installer to be told the tool is already present.

Installation is delegated to the platform's package manager — `winget` or
Chocolatey, Homebrew, `apt`/`dnf`/`pacman` — with one deliberate exception, and
one deliberate escape hatch:

- **The .NET SDK uses Microsoft's official `dotnet-install.sh`.** No package
  manager can pin `10.0.100`; that installer is the only mechanism that takes an
  exact version, and pinning is the whole point. It writes to `~/.dotnet` and
  touches nothing else.
- **Downloaded installers are written to a file and then executed**, never piped
  into a shell. The file is inspectable when something goes wrong, and a
  truncated download cannot half-execute.

`--check` probes everything and installs nothing, exiting non-zero if a required
tool is missing. That is the mode CI and an agent want, and the `test` job runs
it on every pull request, which is what keeps the version parsing honest when
`global.json` moves.

Anything that cannot be completed unattended — starting Docker Desktop, picking
up a new `PATH` entry, joining the `docker` group after a Linux install — is
reported as a numbered manual step. **The script never reports success for
something it did not do.** A green tick that means "installed, but you must log
out before it works" is worse than no tick, because the next failure looks like
a different problem.

## Why not PowerShell

`pwsh` genuinely runs on all three platforms, and on Windows it needs no
bootstrap. It loses on the other two: a macOS or Linux contributor without
PowerShell installed cannot run the script that installs their prerequisites,
which is exactly the situation the script exists to fix. Solving that needs a
documented shell one-liner to install `pwsh` first — at which point the shell,
not PowerShell, is the thing that actually runs first.

## Why not a `.sh` and a `.ps1` pair

Two files is the conventional answer and it is what `gradlew`/`gradlew.bat` do.
It was rejected because two implementations of the same logic drift, and the
half that drifts is always the one the maintainers do not run. Every change here
would need testing on Windows to be trusted, and a wrong-but-plausible
`init-dev.ps1` is worse than no Windows support at all.

Git Bash makes the pair unnecessary. `CONTRIBUTING.md` already requires Git for
Windows with `core.symlinks true` — without it the agent instruction symlinks
arrive as text files containing a path — so every Windows contributor here
already has `sh`. The shell is not a new dependency; it is one this repository
had already taken.

## Why not an `sh`/PowerShell polyglot

A single file that is valid in both parsers solves the bootstrap problem
everywhere and needs no Git Bash. It was rejected as a parser trick: it is still
two implementations, now harder to read, and its correctness depends on
tokenizer details of two shells rather than on anything either language
documents.

## Why not a devcontainer

A `devcontainer.json` gives a genuinely reproducible environment and is the
right answer for a project whose contributors all use one editor. It fails two
requirements here. It needs Docker before it can install Docker, and Docker is
one of the things missing on the machine this script targets. And it binds the
setup to editors that implement the spec, where this repository is deliberately
tool-agnostic — `AGENTS.md` is the canonical instruction file precisely so no
single tool is assumed.

A devcontainer remains a reasonable *addition* later, layered on the same
prerequisites. It is not a replacement for a script that runs on the metal.

## Consequences

- `README.md` and `CONTRIBUTING.md` lead with `./init-dev.sh`; the prerequisite
  table lives in `CONTRIBUTING.md` and names the file that pins each version.
- Bumping `global.json` or the workflow's `node-version` changes what the script
  installs, with no edit to the script. A pull request that changes one of those
  files and also edits a version in `init-dev.sh` has introduced the drift this
  ADR exists to prevent.
- CI runs `shellcheck -s sh init-dev.sh` in `build` and `./init-dev.sh --check`
  in `test`. Both are steps in existing jobs rather than new jobs, because a new
  job is a new status-check context and would have to be added to the ruleset
  before it could be required — see
  [ADR-0011](ADR-0011-ci-contexts-precede-their-checks.md).
- Windows support rests on Git Bash. If that assumption ever breaks, the polyglot
  and the `.ps1` pair above are the two options to reopen, and this ADR should be
  superseded rather than edited.
