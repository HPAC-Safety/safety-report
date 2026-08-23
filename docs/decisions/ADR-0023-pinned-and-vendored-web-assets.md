# ADR-0023 — The web build's inputs are pinned, verified, and vendored

**Status:** Accepted

Supplements [ADR-0006](ADR-0006-theme-engine.md), which chose Tailwind v4's
standalone CLI. This one is about everything that has to arrive from somewhere
else before a stylesheet can exist: the compiler, the typefaces, and the logo.

## Context

The web layer has exactly one build step and no `node_modules`. That leaves
three external inputs, and each one is a way for the build — or the running page
— to reach the network:

1. The Tailwind standalone binary, downloaded by `tools/build-css.sh`.
2. Poppins and Aleo, which hpac.ca loads from `fonts.googleapis.com`.
3. The HPAC logo, which lives in a WordPress media library.

Two constraints decide all three. `AGENTS.md`: *a tool version is pinned in
exactly one file, and scripts read it from there.* And `docs/design-system.md`:
*someone filing a report about a crash should not have that visit logged by a
third party, and the form must work on a bad connection at a landing zone.*

## Decision

### The compiler: one pin file, checksum-verified

`tools/tailwind.pin` holds the version and the SHA-256 of every published
release asset. `tools/build-css.sh` reads both from it; the number appears
nowhere else — not in `ci.yml`, not in `init-dev.sh`, not in a README.

The download is written to a temporary file, hashed, and only then made
executable and moved into place. A mismatch deletes it and fails the build.
A binary already present in `tools/` is re-hashed on every run, which is also
what makes the script idempotent: matching hash, no download.

`TAILWIND_BIN` overrides the whole mechanism for an air-gapped build or a
distribution package. It is used as given and explicitly *not* verified, and the
script says so on the way past — a check the caller has already opted out of is
better stated than faked.

### The typefaces: committed woff2, not a CDN and not a build-time fetch

Poppins and Aleo are committed under `src/web/assets/fonts` with their OFL
licence texts. Both are SIL Open Font Licence 1.1, which permits
redistribution.

The files are the woff2 subsets Google serves, not the TTFs in
`google/fonts`. Same fonts under the same licence, but already hinted, already
compressed, and already split on Google's `unicode-range` boundaries — a latin
Poppins weight is 7.9 KB against roughly 150 KB of TTF. Producing an equivalent
woff2 from the TTF needs `fonttools`/`woff2`, which would put a Python build
dependency into a layer whose whole premise is that it has no build
dependencies. Provenance, the exact URLs, and the refresh procedure are in
`src/web/assets/fonts/README.md`.

### The logo: committed, and flagged

`src/web/assets/hpac-logo.png`, 260×125, from hpac.ca's media library. It is a
placeholder. `docs/design-system.md` already records that no vector or
high-resolution HPAC mark exists publicly and that one is needed before launch;
committing the best available raster does not close that item.

## Alternatives

- **Fetch fonts from `fonts.googleapis.com`.** What hpac.ca does. Rejected
  outright: it makes filing an occurrence report generate a request to Google
  carrying the reporter's IP and referrer, and it makes the form's typography
  depend on a connection the reporter may not have.
- **Download the fonts in `build-css.sh` instead of committing them.** Keeps
  binaries out of git, but the artefacts are ~120 KB total and never change,
  while the cost is a build that fails when Google is unreachable and a
  deployment whose exact bytes are not recorded anywhere. Rejected.
- **Track the fonts with git-lfs.** Rejected: a clone that silently yields
  pointer files instead of fonts is the same failure mode as the symlink
  problem `AGENTS.md` already warns Windows contributors about, for 120 KB of
  saving.
- **`latest` for the Tailwind binary.** Rejected by the issue and by
  `AGENTS.md`. A stylesheet that differs between two builds of the same commit
  is not reproducible, and there is no diff to review when it changes.
- **Pin the version but skip the checksums.** Simpler, and the version alone
  does defeat an accidental upgrade. Rejected because it does not defeat a
  substituted or truncated download, and this script's whole job is to fetch an
  executable off the internet and run it.
- **Fetch `sha256sums.txt` from the pinned release instead of committing the
  hashes.** Tempting — one number to bump. Rejected: the sums would then come
  from the same host as the binary, so it verifies transfer integrity and
  nothing else. Committed hashes are reviewed in a pull request.
- **A `package.json` and `npx tailwindcss`.** Rejected in ADR-0006 and still
  rejected: it reintroduces `node_modules` into a layer that ships as static
  files.

## Consequences

- A clean clone with no `node_modules` and no Node builds the stylesheet with
  `./tools/build-css.sh`. That is checked by the `web` job in CI.
- Bumping Tailwind is one edit to `tools/tailwind.pin`, replacing the version
  and all seven checksums together. The file says so, at the top.
- Renovate does not manage that pin — it has no manager for a bespoke file. The
  version therefore moves when someone moves it, not on a schedule. Filing a
  custom-manager rule is worthwhile follow-up, not part of this change.
- `tools/tailwindcss` and `src/web/styles/site.css` stay gitignored. The
  binary is a cache; the stylesheet is generated.
- Refreshing a typeface is a reviewable commit containing the new bytes, not an
  invisible change in what a CDN serves.
