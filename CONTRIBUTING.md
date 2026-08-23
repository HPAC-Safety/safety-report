# Contributing

Read [AGENTS.md](AGENTS.md), then run:

```bash
./init-dev.sh
dotnet test
```

Work from a GitHub issue on a focused branch. Pull requests must include a
closing reference such as `Closes #74`, pass required checks, receive one
approving review, and use squash merge.

Use Shouldly, keep Core dependency-free, add abstractions only at real external
boundaries, and keep READMEs current and short. Package versions belong in
`Directory.Packages.props`, not individual project references.

Changes involving questions, privacy, prompts, summaries, review, or public
output must use synthetic privacy-contract tests and the
`anonymize-hpac-reports` skill. Never put real report content or credentials in
source, logs, tests, issues, or pull requests.

Report security vulnerabilities privately as described in [SECURITY.md](SECURITY.md).
