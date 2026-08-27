# ADR-0003 — Five-stage anonymization, deterministic first

**Status:** Superseded by the
[one-call specification](../../features/ai-anonymization/ai-anonymization.feature). There is no active
scrub/audit/translation pipeline.

## Context

The core promise of the system is that a published summary cannot identify the
pilot. A single "summarize and remove personal information" model call is the
obvious implementation and the wrong one: the model is simultaneously optimizing
for readability and redaction, and it fails silently.

## Decision

Five stages:

1. Deterministic scrub — no AI. Structured contact fields dropped; emails,
   phones, URLs, member numbers regex-stripped; names replaced; site generalized
   to a region; make and model discarded.
2. Summarize, in the report's own language.
3. PII audit — separate call, separate prompt, reads only the summary, returns
   structured findings.
4. Translate the summary into the other official language.
5. PII audit the translation.

Findings flag for a reviewer; they never silently rewrite.

## Consequences

- Anything a regular expression can remove reliably is removed before a model
  sees it. The scrub lives in `Core` with no dependencies and is provable in a
  plain unit test.
- Stage 5 exists because translation is another generative step that can
  reintroduce a removed detail.
- Two extra model calls per report. At HPAC's volume this is negligible.
- The golden-file suite in `HpacSafety.Anonymization.Tests` is the real
  regression protection, not the coverage percentage.
