---
name: hpac-safety-conventions
description: Repository-wide HPAC Safety conventions. Use for any code, test, documentation, or diagram change in this repository.
---

# HPAC Safety conventions

1. Read [`features/README.md`](../../features/README.md) and the affected canonical
   pages. Treat source/tests as current-state evidence and ADRs/issues as
   history when they conflict.
2. Keep the implementation direct. Add an interface only at a real external
   boundary or when two implementations already need a shared contract.
3. Protect privacy at DTO, storage, model, logging, review, and publication
   boundaries. Use synthetic data only.
4. Use .NET 10, nullable reference types, async APIs for I/O, and cancellation
   tokens at public async boundaries. `Core` has no runtime package dependency.
5. Use `DateOnly` for reported dates, `TimeOnly` for local wall time,
   `DateTimeOffset` for instants, and never `DateTime`.
6. Use Shouldly and Given/When/Then tests. Use Mermaid for diagrams.
7. Put UI copy in locale catalogues. Database questions carry manually authored
   English and French text in each immutable revision.
8. Never log DTO bodies, answers, private context, prompts/responses,
   credentials/tokens, client filenames, or attachment URLs.

Before finishing, run the narrowest relevant checks, inspect the diff for
unrelated changes, and update `/spec` whenever the target design changes.
