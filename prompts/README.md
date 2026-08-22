# Runtime prompts

These files are **application assets, not developer documentation and not agent
skills.** They are loaded by `HpacSafety.Worker` at runtime and sent to the model
as part of processing an incoming report.

The distinction matters. A `SKILL.md` under `skills/` shapes how an AI agent
*writes this codebase*. A prompt here shapes what the model does to a *real
pilot's accident report* in production. Redaction rules belong in this second
category — they are part of the request, and they ship with the application.

## Versioning

Filenames carry a version: `summarize.v2.md`, `pii-audit.v2.md`. **v2 is
current**; the v1 files stay because a summary generated under them has to remain
explicable.

`summaries.prompt_version` and `summaries.model` are stamped on every generated
row, so any published summary can be traced back to exactly what produced it.

**Bump the version rather than editing in place** whenever the change could
alter output. Old versions stay in the repository — a summary generated last
year must remain explicable.

## Composition

`redaction-rules.v2.md` is shared text included by both prompts, so the rules
cannot drift between the summarizer and the auditor. The loader composes:

```mermaid
flowchart LR
    rr["redaction-rules.v2.md"] --> s["summarize.v2.md"]
    rr --> a["pii-audit.v2.md"]
    s --> call1["stage 2 · summarize"]
    a --> call2["stages 3 and 5 · audit"]
```

Each version composes with its own: `summarize.v1.md` includes
`redaction-rules.v1.md` and always will. A version is a frozen pair, not a file
that picks up whatever the shared rules say today.

### What changed in v2

The redaction rules now describe **what the deterministic scrub already did** —
the role words it writes in place of a name, and the `[removed]` marker it leaves
behind — so the model neither strips "the pilot" as though it were a name nor
reproduces a marker in a published summary. See
[ADR-0028](../docs/decisions/ADR-0028-role-words-in-place-of-names.md).

## Changing these files

A prompt change is a change to what gets published about real accidents. Treat
it as you would a change to redaction code:

- Bump the version.
- Run the golden-file suite in `tests/HpacSafety.Anonymization.Tests`.
- Have `agents/anonymization-auditor.md` review the diff — it specifically
  checks for instructions softened from "never" to "avoid".

## Related

- `docs/anonymization-policy.md` — the policy these prompts implement
- `docs/aircraft-classification.md` — the class vocabulary they use
