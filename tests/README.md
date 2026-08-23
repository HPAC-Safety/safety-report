# Tests

- `HpacSafety.Core.Tests`: domain rules.
- `HpacSafety.Infrastructure.Tests`: mapping, migration, encryption, and seed.
- `HpacSafety.Anonymization.Tests`: DTO partition and one-call prompt boundary.
- `HpacSafety.Api.Tests` and `HpacSafety.Worker.Tests`: deployable scaffolds.
- `js`: repository tooling.
- `e2e`: future browser journeys.

```bash
dotnet test
dotnet test --filter "Category!=Integration"
node --test $(find tests/js -name '*.test.mjs')
```

Integration tests require Docker. Use Shouldly and
`Given_..._When_..._Then_...` names. Privacy tests use invented identifiers,
prove the private value reached model context, prove it did not survive in the
candidate output, and prove useful non-private content remained. Never use real
report content.
