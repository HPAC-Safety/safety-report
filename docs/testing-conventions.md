# Testing conventions

- Use xUnit and Shouldly for .NET, and `node:test` for JavaScript.
- Name .NET tests `Given_..._When_..._Then_...`.
- Keep Core tests free of databases and networks.
- Mark Docker/PostgreSQL tests with `Category=Integration`.
- Test behavior and privacy boundaries rather than implementation layers.
- Never use real report content.

Anonymization tests use synthetic private values. They prove that private and
non-private answers reach the correct arrays, skipped questions are absent, the
runtime prompt requires a matching identity to become a role such as “the
pilot,” the translation provider only ever receives anonymized summary text,
and a removed PII-audit/scrubber/classifier port does not return.

Run:

```bash
dotnet test
dotnet test --filter "Category!=Integration"
node --test $(find tests/js -name '*.test.mjs')
```

Integration tests require Docker. Do not weaken an assertion merely to retain a
removed subsystem or satisfy a coverage number.
