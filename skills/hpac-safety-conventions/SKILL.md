---
name: hpac-safety-conventions
description: Repository-wide HPAC Safety conventions. Use for any code, test, documentation, or diagram change in this repository.
---

# HPAC Safety conventions

1. Before implementing, orient with `graphify query`/`graphify explain`
   (when `graphify-out/graph.json` exists) and read
   [`features/README.md`](../../features/README.md), the affected canonical
   pages, that area's **out of scope** section, any ADR that bears on the
   change, and [`docs/lessons/`](../../docs/lessons/README.md) — do this even when the task
   looks small or familiar; drift comes from skipping this step, not from
   the change itself. Treat source/tests as current-state evidence and
   ADRs/issues as history when they conflict.
2. If that reading surfaces a gap — a decision the existing ADRs/feature
   files/skills don't settle, or two of them in tension — stop and ask
   before proceeding. Do not resolve it by assumption or by picking the
   option that looks most convenient to implement, even under a "spike" or
   "just get something working" framing. Name the gap, the options, and a
   recommendation if you have one.
3. Write the specification before the implementation. When behavior is added
   or changed, the scenario in `features/<area>/<area>.feature` is authored or
   amended in the same pull request, and the implementation is what makes it
   pass. When an implementation turns out to do the wrong thing, ask first
   whether the scenario said the wrong thing and correct it there — a
   correction argued in conversation leaves no artifact
   ([ADR-0083](../../docs/decisions/ADR-0083-specification-driven-development.md)).
   Say what is out of scope for the area you touched, not only what you built.
4. Keep the implementation direct. Add an interface only at a real external
   boundary or when two implementations already need a shared contract. When
   a real seam or a second concrete case does justify structure, reach for
   SOLID and named Gang-of-Four patterns as the vocabulary — but the seam
   earns the pattern; naming a pattern never earns the seam.
5. Protect privacy at DTO, storage, model, logging, review, and publication
   boundaries. Use synthetic data only.
6. Use .NET 10, nullable reference types, async APIs for I/O, and cancellation
   tokens at public async boundaries. `Core` has no runtime package dependency.
7. Use `DateOnly` for reported dates, `TimeOnly` for local wall time,
   `DateTimeOffset` for instants, and never `DateTime`.
8. Use Shouldly, and name tests `GivenX_WhenY_ThenZ`
   ([ADR-0069](../../docs/decisions/ADR-0069-scannable-given-when-then-test-names.md)).
   Use Mermaid for diagrams.
9. Put UI copy in locale catalogues. Database questions carry manually authored
   English and French text in each immutable revision.
10. Never log DTO bodies, answers, private context, prompts/responses,
   credentials/tokens, client filenames, or attachment URLs.

Before finishing, run the narrowest relevant checks, inspect the diff for
unrelated changes, and update `/features` whenever the target design changes.
