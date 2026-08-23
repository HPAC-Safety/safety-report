# ADR-0037 — Progressive agent instructions

**Status:** Accepted and narrowed by issue #78. `/spec` is the product authority,
and conflicting generic design/pattern skills were pruned.

## Context

`AGENTS.md` had grown to 691 lines. It combined the safety invariants every agent
must see with detailed rules for tests, localization, persistence, media, web
design, Terraform, documentation, and pull-request delivery. Every task paid the
context cost, while task-specific rules were difficult to discover as a unit or
reuse from another agent entry point.

The repository already uses `skillfile` and local skills, but
`hpac-safety-conventions` duplicated several `AGENTS.md` sections and had become
another broad instruction file. The public skill registries also contain
maintained guidance that should not be re-authored locally when it fits.

## Decision

Keep `AGENTS.md` as the always-loaded safety contract. It owns:

- the system identity and data sensitivity;
- the product/privacy invariants;
- specification authority and the material-ambiguity rule;
- the distinction between agent skills and runtime prompts;
- a routing table to focused skills and canonical documentation;
- the minimum issue/PR completion contract.

Move detailed procedures behind focused, triggerable skills:

| Former `AGENTS.md` concern | Owner |
|---|---|
| Requirement gaps and how to ask | `clarify-hpac-requirements` |
| Test conventions and safety assertions | `test-hpac-safety` |
| English/French parity and localization | `localize-hpac-app` |
| Question bank, lifecycle, outbox, privacy | existing `incident-domain-model` |
| Database and migration rules | `persist-hpac-data` |
| Upload and media privacy | `handle-hpac-media` |
| Static UI, theme, and browser assets | `build-hpac-web-ui` |
| Terraform, AWS, metrics, and topology | `manage-hpac-infrastructure` |
| General code and prompt boundaries | narrowed `hpac-safety-conventions` |
| General design advice | Plain code by default; add boundaries only for a current need |
| Documentation, issues, PRs, and CI | `deliver-hpac-change` |

Use the maintained upstream `addyosmani/agent-skills` skill
`documentation-and-adrs` for general documentation practice. Keep HPAC's stricter
ADR, README, issue-closing, and CI rules in `deliver-hpac-change`.

## Registry review

The audit searched the public registries for requirement clarification,
architecture decision records, localization, and pull-request/CI workflow.

- `addyosmani/agent-skills:documentation-and-adrs` fits the repository's existing
  documentation philosophy, defers to established repository conventions, and
  comes from a source already pinned here. It is added upstream.
- The shortlisted requirement skills were product-discovery and specification
  workflows, not a safety-critical implementation ambiguity policy.
- The shortlisted localization skill assumed React/Vue and en-US/zh-CN, which
  conflicts with this static EN-CA/FR-CA application and its split between UI
  chrome and database-authored questions.
- The shortlisted GitHub workflow skills were generic automation workflows and
  did not encode this repository's closing-keyword, squash, documentation, and
  green-CI contract.

The latter three concerns therefore remain local and explicitly HPAC-scoped.

## Alternatives

- **Keep expanding `AGENTS.md`.** This preserves one file but loads hundreds of
  irrelevant lines for every task and makes focused guidance hard to trigger.
- **Move everything into one conventions skill.** This reduces always-on context
  but recreates the same mixed-concern document one level later.
- **Import generic skills for every concern.** This reduces local maintenance but
  introduces framework, locale, and workflow defaults that contradict recorded
  HPAC requirements.
- **Move safety invariants into skills too.** Rejected because skill activation
  is conditional; publication and privacy constraints must always be visible.

## Consequences

- Agents load a short safety contract on every task and detailed guidance only
  when its trigger applies.
- Every former section has an explicit owner and can evolve independently.
- Local skill count increases, but each skill is narrow, HPAC-specific, and
  declaratively installed from `Skillfile`.
- The routing table and skill descriptions become part of correctness: a new
  cross-cutting concern must update the route rather than silently expanding
  `AGENTS.md` again.
- `Skillfile.lock` pins the upstream documentation skill, so updates remain
  deliberate and reviewable.
