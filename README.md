# HPAC Safety Occurrence Reporting

A bilingual incident-reporting system for the Hang Gliding and Paragliding
Association of Canada. The repository is still a scaffold; the domain and
persistence foundations exist, while the API, Worker, and browser flows remain
open work.

## Product flow

1. Read the latest active revision of each database question in `SortOrder`.
2. Show its English or French wording. Every answer is optional except the
   yes/no publication-consent question.
3. Save the report DTO, exact question revision ids, answers (including skips),
   and Worker outbox message atomically.
4. The Worker queries those questions, answers, and privacy flags into one DTO.
5. Cloudflare Turnstile verifies the submission server-side before it reaches
   the database.
6. One model call summarizes non-private answers and uses private answers only
   to recognize identifying details. A matching pilot name becomes “the pilot.”
7. The Worker translates that candidate into the report's other official
   language and stores both. A safety officer reviews both candidates.
   Publication requires positive consent and human approval of each.
8. The Worker notifies `safety@hpac.ca` that a report is ready for review.

Question revisions are complete immutable records: English, French, input type,
options, order, privacy, active state, and display metadata. Any change creates
a new row. Question wording is authored in one language and translated into the
other before the row is saved. The system has no separate PII-audit call,
deterministic scrubber, or publication-channel framework. Aircraft
classification is not a subsystem — it is two ordinary questions, a public
certification class and a private make/model.

See [AGENTS.md](AGENTS.md) for invariants, [docs/form-spec.md](docs/form-spec.md)
for the current Typeform questions, and
[summarize.v4.md](src/HpacSafety.Worker/Prompts/summarize.v4.md) for runtime
model behavior.

## Projects

- `HpacSafety.Core`: immutable questions, reports, answers, summaries, outbox.
- `HpacSafety.Infrastructure`: EF Core, PostgreSQL, encryption, migrations, seed.
- `HpacSafety.Api`: HTTP scaffold.
- `HpacSafety.Worker`: outbox-consumer scaffold.
- `src/web`: static public and admin UI scaffold.
- `infra`: minimal AWS deployment in `ca-central-1`.

## Development

```bash
./init-dev.sh
dotnet test
```

Integration tests use Testcontainers and require Docker. The repository pins
.NET in `global.json`; use `./init-dev.sh --check` to diagnose prerequisites.

Regenerate the source form specification with:

```bash
python3 tools/extract-typeform.py
```

Never place real report content in logs, tests, issues, or pull requests.
