---
title: The claims are generated as JSON, a graph fragment, and one slim matrix
description: tools/spec/generate-traceability.ts writes .spec/claims.json, the canonical data for tools and CI, and one slim .spec/traceability.md for people, replacing the block-per-claim matrix and .spec/bindings.md; tools/spec/graph-fragment.ts merges the same data into the local graphify graph as semantic-tier nodes, so claims, ADRs, and lessons are queryable with no LLM pass.
type: adr
status: superseded
date: 2026-10-03
decision-makers: Chase Florell
keywords: traceability, claims, JSON, JSON Schema, generated file, step bindings, graphify, knowledge graph, merge, ADR-0084, ADR-0088, ADR-0101, ADR-0106, ADR-0183, ADR-0184
---

# ADR-0193 — The claims are generated as JSON, a graph fragment, and one slim matrix

**Status:** Superseded. Decided by the owner on 2026-10-03 in
[#810](https://github.com/HPAC-Safety/safety-report/issues/810), part of
[#809](https://github.com/HPAC-Safety/safety-report/issues/809). Supersedes
[ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md),
[ADR-0088](ADR-0088-the-matrix-carries-the-specification-into-the-graph.md),
[ADR-0106](ADR-0106-every-line-of-the-matrix-derives-from-one-source-item.md),
and
[ADR-0184](ADR-0184-a-generated-map-binds-every-claim-to-its-step-definitions.md).
Superseded by
[ADR-0194](ADR-0194-a-split-area-keeps-every-claim-id-and-a-new-claim-takes-the-new-areas-prefix.md).

## Context

Two generated Markdown files described the specification:
`.spec/traceability.md` (3,326 lines) and `.spec/bindings.md` (3,449 lines).
Both were machine-shaped, about 4.4 lines per claim, mostly headings. People
could not read them, and tools could only grep them.

They also did not reach the knowledge graph the way ADR-0088 intended.
graphify 0.9.69 (read in its source):

- never reads a `.feature` file;
- skips data-shaped JSON (`extractors/json_config.py` indexes only manifests
  and files with a top-level `$schema`, `$ref`, `extends`, `dependencies`, or
  `compilerOptions`);
- gives a Markdown file only its headings without an LLM pass.

So the graph knew a claim only as a heading in a generated file, with no edge
to its scenario, its tests, or the ADRs and lessons that cite it. The local
graph lacked ADR-0190, lesson 0043, and REQ-WLD-049 until someone re-ran the
semantic extraction.

## Decision drivers

- One canonical, machine-readable record of every claim, for tools and CI.
- A matrix a person can read in one screen per area.
- Every claim, ADR, and lesson findable in the local graph with no LLM pass,
  and still there after `graphify update`.
- Generated files that git merges when branches change different claims.
- graphify is not forked, and the generators run with no `npm install`.

## Considered options

- **Keep `traceability.md` and `bindings.md`, and add JSON beside them.**
  Rejected: three generated copies of one fact, and the two Markdown files
  stay unreadable.
- **Inject the fragment with `graphify merge-graphs`.** Rejected: it prefixes
  every node ID with a repository tag and composes undirected graphs for a
  cross-repository view, so merged into this repository's own graph it would
  duplicate every file node instead of joining it.
- **Seed graphify's extraction cache.** Rejected: the AST cache is namespaced
  by graphify's version and the semantic cache by a fingerprint of its LLM
  prompt, so a seeded entry is silently dropped on the next upgrade, and
  `graphify update` never reads the semantic cache at all.
- **Track the graph fragment.** Rejected: it is a pure function of
  `claims.json`, so tracking it would double every merge conflict for nothing.
- **Fork graphify to read `.feature` files.** Rejected, as ADR-0088 already
  did; the fragment needs no change to graphify.
- **A JSON Schema library.** Rejected: the generator runs from the base branch
  with no `npm install`, and the keywords the schema uses fit in 80 lines.
- **`claims.json`, one slim matrix, and semantic-tier nodes written straight
  into `graph.json`** — chosen.

## Decision

1. **`.spec/claims.json` is the canonical generated data.** One file holds:
   - every claim: ID, area, feature file, title, `Rule`, tags, engine,
     status, every step with the files whose definitions bind it, whether a
     step is ambiguous, whether an `@ignore` is stale, the constraints it
     verifies, and the ADRs and lessons that cite it;
   - every `CON-*`, with its page and the claims that verify it;
   - every ADR and lesson, with the claims, constraints, and ADRs it cites,
     the ADRs it supersedes or amends (read from its status paragraph), and a
     lesson's skills;
   - the ambiguous steps and unused step definitions.

   Each claim, record, and step sits on lines of its own, with keys in a
   fixed order and no totals, so git merges two branches that change
   different claims (ADR-0106). It records no line numbers: #810 asked for
   each claim's file and line, and the owner decided on 2026-10-03 to keep the
   file only, because a line number moves every claim below an inserted
   scenario, and two branches editing one feature file would then conflict.
   What needs a line reads it from the feature file. `.spec/claims.schema.json` is its JSON Schema;
   the generator validates against it, with a dependency-free validator
   (`tools/spec/json-schema.ts`) because `traceability.yml` runs with no
   `npm install`.

2. **`claims.json` carries no top-level `$schema` key.** graphify indexes a
   JSON file with one as configuration, and would turn the claims into
   meaningless key nodes; the fragment below is the graph's view of them. The
   schema file does carry `$schema` and is indexed, which is harmless.

3. **One slim matrix for people.** `.spec/traceability.md` is one table row per
   claim — ID, title, area, engine, status, bound step files — and one per
   constraint, sorted by ID, with no totals. `.spec/bindings.md` is deleted.

4. **One generator.** `node tools/spec/generate-traceability.ts` writes both
   files; `--check` fails on drift; it fails on every condition either old
   generator failed on: a duplicate, malformed, missing, or dangling ID, a
   tagged `Rule`, an unreadable step definition, and a built claim with a step
   no definition binds. `--no-fail` writes without failing, for the hooks and
   the bot. Step resolution moved unchanged to `tools/spec/step-bindings.ts`,
   and the claim readers to `tools/spec/read-claims.ts`.

5. **A deterministic graphify fragment.** `node tools/spec/graph-fragment.ts`
   builds, from `claims.json`, one node per claim, constraint, ADR, lesson, and
   feature area, labelled by its ID (`REQ-WLD-049`, `ADR-0190`,
   `Lesson 0043`), with the claim's step text on its node, and typed edges:
   claim `specified_in` area, claim `bound_by` step-definition file, constraint
   `verified_by` claim, ADR or lesson `cites` claim, constraint, or ADR, ADR
   `supersedes` or `amends` ADR, lesson `updates` skill, and each record
   `documented_in` its page. A claim node's line is read from its feature file
   at merge time. It merges them into the local `graphify-out/graph.json`.
   graphify is not forked (ADR-0088).

6. **Where the merge runs.** `.githooks/post-merge` and `post-rewrite` after
   regenerating; `init-dev.sh` after building the graph, falling back to
   `graphify update` when no LLM backend is available. On the primary
   checkout's `main` the hooks still regenerate and stage nothing, but they do
   run the merge (owner decision, 2026-10-03): it writes only the untracked
   `graphify-out/`, so `main` stays exactly `origin/main`, as #802 requires,
   and a pull is when new claims arrive. `generate-traceability.ts --fragment
   <path>` writes the same extraction to a file.

### How the fragment survives graphify (graphify 0.9.69)

Every node and edge carries `_origin: "semantic"` and `spec_fragment: true`.
`graphify update`, full or incremental (as the post-commit hook runs it),
re-extracts only the AST tier and keeps a semantic item while its
`source_file` exists. A merge first drops every `spec_fragment` item, so it
never accumulates stale claims. Which `source_file` each node carries decides
the rest:

- **Never a Markdown page.** graphify treats a `.md` file carrying any
  non-AST node as already covered by its LLM pass and stops re-scanning it,
  so an ADR, lesson, or constraint page given a fragment node would never
  get a new heading again, and a newly pulled one never its file node.
- **Never a file graphify extracts.** An incremental rebuild of a changed
  source evicts every node naming it, semantic or not; with
  `.spec/claims.json` as the source, every commit that changed it dropped
  the records, and graphify's shrink guard then refused to write the graph
  at all.
- So a claim and an area name their `.feature` file, which graphify has no
  extractor for, and an ADR, lesson, or constraint names the directory it
  lives in (`.spec/decisions`, `.spec/lessons`, `.spec`); its own page is in
  `path`, and in `source_location` so a query shows it.

Verified on this repository: every fragment node and edge survived a full
`graphify update .` and an incremental rebuild of `claims.json`, an ADR, a
constraint page, a feature file, and a new lesson page, and the new page got
its file node and headings, refreshed again when it changed.

### What still holds of the records this supersedes

- [ADR-0084](ADR-0084-stable-claim-ids-and-a-generated-traceability-matrix.md): every scenario carries one stable `@REQ-*` claim ID and every
  normative constraint a `CON-*` ID naming the claims that verify it; an ID is
  never reused or renumbered; a duplicate, malformed, missing, or dangling ID
  fails the build.
- [ADR-0088](ADR-0088-the-matrix-carries-the-specification-into-the-graph.md): graphify is not forked, and it never reads a `.feature` file.
- [ADR-0106](ADR-0106-every-line-of-the-matrix-derives-from-one-source-item.md): decision 4, a rejected push that lost to a newer head is a
  superseded run (as amended by
  [ADR-0113](ADR-0113-a-bot-pushing-onto-a-pull-request-replays-past-another-bot.md));
  no totals in a generated file; items in ID order.
- [ADR-0184](ADR-0184-a-generated-map-binds-every-claim-to-its-step-definitions.md): the rules by which each runner's step definitions are matched; the
  specification is the authority; a built claim with an unbound step fails,
  while stale `@ignore` claims, ambiguous steps, and unused step definitions
  are recorded, not failed; a large area is grouped with `Rule:` blocks.

## Consequences

- `graphify query "REQ-WLD-049"`, `"ADR-0190"`, and `"lesson 0043"` start at
  their own nodes on a graph built with `graphify update` alone.
- An edge to a file graphify has not indexed yet, such as a page pulled since
  the last `graphify update`, is dropped, not invented. The first merge after
  graphify indexes the file adds it: the next post-merge or post-rewrite
  hook, or `node tools/spec/graph-fragment.ts` by hand.
- Every Markdown page stays under graphify's own heading scan; the fragment
  adds nodes beside it and never replaces it.
- `claims.json` merges without a driver when two branches change different
  claims, including one inserting a scenario into a feature file another
  branch edits; a test proves that case. Two branches changing the same claim,
  or claiming the same new ID, still conflict.
- The matrix is a table, so two branches that change neighbouring rows
  conflict in git. `merge=ours` and the post-merge and post-rewrite hooks
  resolve that locally by regenerating.
- `check-feature-coverage` reads the claims an exemption cites from
  `claims.json`, counting only declared claims, not every ID it mentions.
- `traceability.yml` commits all three generated files; the `docs` job runs
  `generate-traceability.ts --check`; `tools/spec/check-generated-file.ts` is
  deleted, having no other caller.

## Related

- [ADR-0101](ADR-0101-ci-regenerates-the-traceability-matrix.md): CI
  regenerates the generated files onto a same-repo pull request; unchanged.
- [ADR-0183](ADR-0183-the-specification-lives-in-a-spec-directory.md): the
  specification and its generated files live in `.spec/`; unchanged.
- [#802](https://github.com/HPAC-Safety/safety-report/issues/802): a hook never
  leaves a tracked change on `main`.
