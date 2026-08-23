# Agent workflow

`AGENTS.md` is the canonical repository instruction file and is symlinked for
other supported agents. `Skillfile` installs one HPAC-specific local skill:
`anonymize-hpac-reports`. Runtime LLM behavior belongs in
`src/HpacSafety.Worker/Prompts/`, never in an agent skill.

Work from a GitHub issue on a branch, preserve unrelated changes, write tests
before behavior changes, and open a pull request that closes the issue. Keep
the implementation plain and update the issue when the accepted approach
changes.

Generated files have one owner:

- `docs/form-spec.md`: `python3 tools/extract-typeform.py`
- `src/web/styles/site.css`: `./tools/build-css.sh`
- EF migrations: `dotnet ef migrations add`
- installed local agent links: `skillfile install`

Never paste real report content into an agent conversation, test, issue, or pull
request.
