---
title: The generated matrix carries the specification into the graph
description: graphify cannot ingest a .feature file and this repository does not fork it; docs/traceability.md is markdown, so the claims reach the graph through the matrix instead.
type: adr
status: accepted
date: 2026-09-22
decision-makers: Chase Florell
keywords: graphify, knowledge graph, traceability, Gherkin, ingestion, vendoring
---

# ADR-0088 — The generated matrix carries the specification into the graph

## Status

Accepted.

## Context

[ADR-0083](ADR-0083-specification-driven-development.md) says the specification
is what an agent is handed, and this repository's agents are told to orient with
`graphify query` before writing code. The graph therefore ought to contain the
specification as well as the code — otherwise the answer to "which claim does
this symbol serve" is a document lookup rather than a query, which is the
spreadsheet problem
[ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)
exists to end.

`GRAPH_REPORT.md` reported all eight `.feature` files as unclassified. The cause
is not configuration: `graphify.detect.DOC_EXTENSIONS` is a hardcoded set —
`.md`, `.mdx`, `.qmd`, `.skill`, `.txt`, `.rst`, `.html`, `.yaml`, `.yml` — with
no extension hook, no configuration file, and no per-repository override.
`.feature` is not in it, and `graphify --help` exposes nothing that would add it.

The 142 markdown files in the repository *are* ingested, which is the useful
half of the finding.

## Decision

**This repository does not fork, patch, or vendor graphify to teach it
Gherkin.** The generated matrix is the bridge instead.

[`docs/traceability.md`](../traceability.md) is markdown, so it enters the graph
like any other document, and it carries exactly what a `.feature` file would
contribute: every claim ID, the area it belongs to, the scenario that states it,
the engine that executes it, and whether it is covered or planned — plus the
constraints and what verifies them. A question about a claim is answerable from
the graph through the matrix.

Two consequences are recorded rather than worked around:

- **A claim reaches the graph one step later than the code does.**
  `graphify update .` is AST-only and re-extracts code; a markdown change needs
  the semantic pass, which costs a model call. A newly tagged scenario is in the
  matrix immediately and in the graph at the next semantic extraction.
- **Scenario step text is not in the graph.** The matrix carries the claim and
  its scenario name, not its `Given`/`When`/`Then` lines. For the step text, the
  `.feature` file is still the source, and the claim ID is how you find it.

## Consequences

- `AGENTS.md` and [`docs/agent-workflow.md`](../agent-workflow.md) say where
  claims live in the graph, so an agent does not conclude the specification is
  missing from it.
- If graphify later gains an extension hook, adding `.feature` becomes a
  configuration change with this record explaining what it replaces.
- Nothing in the repository depends on a graphify version's internals.

## Alternatives

- **Fork or patch graphify to add `.feature` to `DOC_EXTENSIONS`.** Rejected:
  it makes every upgrade a merge, and buys a Gherkin parse the matrix already
  summarizes. The tool is a dependency, not something this repository owns.
- **Generate a sidecar `.md` copy of every feature file purely for ingestion.**
  Rejected: that is a second full copy of the specification, kept in step by a
  generator, for a graph that already has the claim through the matrix. The
  duplication buys step text and costs a drift surface.
- **Rename `.feature` files to `.feature.md`.** Rejected outright: Reqnroll's
  glob, `playwright-bdd`'s `features` pattern, the `cucumber` job, and every
  editor's Gherkin support key off the extension. Breaking the executable
  specification to please a document indexer inverts which one matters.
- **Do nothing and leave the gap unexplained.** Rejected: the next person reads
  "unclassified: .feature 8" in the report and re-investigates.

## Related

- [ADR-0049](ADR-0049-reqnroll-for-executable-gherkin-scenarios.md)
- [ADR-0083](ADR-0083-specification-driven-development.md)
- [ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md)
