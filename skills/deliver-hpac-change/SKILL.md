---
name: deliver-hpac-change
description: Deliver an HPAC safety-report change from issue through a green pull request while capturing decisions, requirements, slice documentation, generated artifacts, skill sources, and tool pins. Use when planning a repository change, writing an ADR or README, managing Skillfile, committing, opening a PR, investigating CI, or reporting completion.
---

# Deliver the whole change

Use upstream `documentation-and-adrs` for general documentation quality and
follow this repository-specific workflow where it is stricter.

## Start from durable work

1. Work from one issue per pull request where practical; create the issue first
   if none exists.
2. Branch from current `main`. Never push directly to protected `main`.
3. Search `skillfile search "<topic>"` and read candidates before writing a
   general-purpose skill. Prefer maintained upstream guidance; keep local skills
   HPAC- or repository-specific.
4. Capture a clarified requirement in the same pull request so it is never
   answered twice.

## Document while implementing

- Write `docs/decisions/ADR-NNNN-<slug>.md` when choosing between viable options
  someone could reasonably question in six months. Continue the sequence,
  record rejected alternatives and consequences, never renumber or delete, and
  supersede a reversed decision with a new ADR.
- Put code-writing rules in `AGENTS.md` or a focused skill, running-system
  behaviour in `docs/` plus an ADR when a choice was made, and runtime model
  input in a new version under `prompts/`.
- Update the README of every changed project, namespace with real behaviour, or
  feature area. State what it owns, excludes, how it is exercised, and how it
  deploys when applicable.

## Respect generated files and pins

- Regenerate `docs/form-spec.md`, `locales/fr-CA.json`, `locales/fr-CA.meta.json`,
  `.claude/skills/`/`.claude/agents/`, and `src/web/styles/site.css` through
  their documented owners; never hand-edit or commit generated `.claude/` files.
- Commit `Skillfile` and `Skillfile.lock` after `skillfile install`.
- Keep each tool version in one canonical pin: .NET in `global.json`, Node in
  `.github/workflows/ci.yml`, Tailwind and hashes in `tools/tailwind.pin`,
  Terraform in `infra/.terraform-version`, and tflint in
  `infra/.tflint-version`. Make scripts and workflows read the pin.
- Make `init-dev.sh` report manual steps honestly; never report success for work
  it could not complete unattended.

## Finish through CI

1. Validate proportionally, including `skillfile validate`, install, and status
   when agent configuration changes.
2. Use a squash-ready PR title and put `Closes #<number>` on its own line in the
   body. Answer the PII/anonymization checklist honestly.
3. Do not create `CODEOWNERS` or add `Co-Authored-By` trailers.
4. Watch every required check. Fix the cause, never lower or skip the gate.
   Investigate apparently unrelated failures before calling them pre-existing
   or flaky; a rerun is diagnosis only when the flake is explained.
5. Report completion only when all checks are green. Otherwise report exactly
   which checks are still running or failing and why.
