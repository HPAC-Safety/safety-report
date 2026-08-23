# Architecture decision records

One file per decision that would otherwise be re-litigated. Each records what
was chosen, what it was chosen over, and why — so a future contributor (human or
agent) can tell the difference between a considered decision and an accident.

Format: context, decision, consequences. Status is `Accepted` unless superseded.

The current anonymization design is
[ADR-0038](ADR-0038-question-privacy-and-llm-anonymization.md). ADR-0003,
ADR-0027, and ADR-0028 remain as historical records of the retired
deterministic scrub.

Related process rules — when an ADR is warranted, what else has to be written
alongside it, and how to deliver the change — are in
`skills/deliver-hpac-change/SKILL.md`. `AGENTS.md` routes every ADR task there.
