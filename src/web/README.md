# web

The two static front ends. **Deployable**, independently of the API and of each
other.

Plain HTML and JavaScript. No SPA framework, no bundler, no `node_modules` at
runtime. Tailwind v4 via the standalone CLI is the only build step, and it
produces one CSS file.

## Layout

| | |
|---|---|
| `public/` | The occurrence report form. Anonymous, bilingual, the highest-traffic surface. |
| `admin/` | The review queue. Authenticated, used by a handful of safety officers. |
| `shared/` | `api-client.js`, `i18n.js`, shared components |
| `styles/` | `tailwind.css` (source, with `@theme` tokens) → `site.css` (generated), and `theme-preview.html` |
| `assets/` | Logo and self-hosted Poppins and Aleo. [`assets/README.md`](assets/README.md) |

## Why plain HTML

A pilot fills this in after a crash, often on a phone, often on a bad connection
at a landing zone. Every kilobyte of framework is a kilobyte between them and
filing the report. There is no client state worth a framework — it is a form and
a list.

Fonts are self-hosted rather than loaded from Google, so filing a report makes
no third-party request. That is checked in CI, not trusted:
[`assets/fonts/README.md`](assets/fonts/README.md) has the provenance and the
refresh procedure.

## The theme

Everything visual starts in `styles/tailwind.css`, and two rules come with it.

**Raw hex values live in that file and nowhere else.** Markup writes
`bg-surface`, `text-ink`, `border-rule` — never `#f22312`, never
`bg-[#f22312]`. The `web` job in CI greps for it.

**Dark mode is those same tokens, redefined — there is no `dark:` variant in
any markup here, and none should be added.** `bg-surface` compiles to
`var(--color-surface)`, so redefining the variable re-themes every existing use
and every use added later. Write the light class; dark mode is already handled.
Three states are supported: no `data-theme` attribute follows
`prefers-color-scheme`, `data-theme="light"` stays light, `data-theme="dark"` is
dark. See [ADR-0024](../../docs/decisions/ADR-0024-dark-mode-is-a-token-redefinition.md).

Token names are roles: `--color-ink` means "body text", `--color-surface-3`
means "third step of the elevation ramp". Adding a colour means adding it to
`@theme` **and** to both dark blocks, which sit adjacent for that reason.
[`docs/design-system.md`](../../docs/design-system.md) lists them with their
provenance.

### theme-preview.html

`styles/theme-preview.html` shows every token, both typefaces, the focus ring
and the light/dark flip on one page. It is a **developer artefact**: not
deployed, not linked from either site, and carrying no user-facing copy — every
string on it is a token name or a font name. It is therefore the one file under
`src/web` that the hardcoded-string lint (#8) must skip.

```bash
./tools/build-css.sh
python3 -m http.server 8080 --directory src/web
# http://localhost:8080/styles/theme-preview.html
```

## Building

```bash
./tools/build-css.sh          # downloads the pinned Tailwind binary, emits styles/site.css
./tools/build-css.sh --watch  # rebuild on change
./tools/build-css.sh --check  # report the version, asset and paths; build nothing
```

The Tailwind version is pinned in `tools/tailwind.pin` and **nowhere else**,
along with the SHA-256 of every release asset. The script verifies the download
against it before making it executable, and re-verifies an existing binary on
every run — which is what makes a second run download nothing. `TAILWIND_BIN`
points it at a binary you supply instead, for an air-gapped build; that one is
used as given and is not verified, and the script says so.

`styles/site.css` and the downloaded `tools/tailwindcss` are gitignored. There
is nothing else to build — the HTML and JS ship as written, and no `node_modules`
exists at any point. See
[ADR-0023](../../docs/decisions/ADR-0023-pinned-and-vendored-web-assets.md).

## Running locally

Any static server, pointed at `public/` or `admin/`:

```bash
python3 -m http.server 8080 --directory src/web/public
```

Set the API base URL in `shared/api-client.js` via the `data-api-base` attribute
on the page, so the same files work against local and deployed APIs without a
build-time substitution.

## Deployment

Two separate static sites. Both are pure static assets — upload and done.

### The host must support URL rewrites

This is a hard requirement, not a preference. We want clean URLs —
`/report`, `/report/thanks`, `/admin/queue` — served from the underlying HTML
without a `.html` suffix and without a hash router.

That rules out **GitHub Pages**, which offers no rewrite mechanism. Its only
approximation is the 404-page trick, which serves a real HTTP 404 before the
page loads: bad for search indexing, worse for a form that should look reliable
to someone filing after an accident.

Viable hosts, with the mechanism each uses:

| Host | Rewrites via |
|---|---|
| CloudFront + S3 | CloudFront Function on viewer-request, or S3 routing rules |
| Cloudflare Pages | `_redirects` with `200` status rewrites |
| Azure Static Web Apps | `staticwebapp.config.json` `navigationFallback` + `routes` |
| Netlify | `_redirects` |

Whichever is chosen, the rewrite config lives **in this repository** next to the
files it serves, not in a console.

Two rules for the rewrites themselves:

- A rewrite serves content at the requested URL — status **200**, not a 301 to
  a `.html` address. The clean URL is the canonical one.
- **No blanket SPA fallback.** A genuinely unknown path must still return 404.
  This is not a single-page app, and rewriting everything to `index.html` turns
  a typo into a silently blank form.

```bash
./tools/build-css.sh
aws s3 sync src/web/public/ s3://<bucket>/ --delete   # example
```

**Deploy them separately.** The public form and the admin surface have different
audiences and different risk. Keeping them on separate origins means the admin
UI can sit behind additional network controls without affecting a pilot's
ability to file a report.

**Because they are split from the API**, two things must be configured:

- **CORS** on the API for both origins.
- **Cookie attributes** — the admin session is cross-origin, so `SameSite` and
  `Secure` must be set for that topology. Getting this wrong fails closed
  (nobody can log in), which is the right direction to fail.

**Cache:** long max-age on `assets/` and `site.css` (content-hashed), **no-cache
on the HTML**, so a form fix reaches reporters immediately.

**Deploy trigger:** GitHub Actions on merge to `main`. A static deploy is fast
and trivially rollback-able — redeploy the previous commit.

## Conventions

- **No hardcoded user-facing strings.** Every label, error, and `aria-label`
  comes from `locales/`. This includes the admin UI.
- **No raw hex in markup.** Use the `@theme` tokens in `styles/tailwind.css`.
- **No `dark:` variants.** Dark mode is a token redefinition; see above.
- Real focus rings, 44px touch targets, WCAG AA contrast. `:focus-visible` and
  `prefers-reduced-motion` are handled once in the base layer, so no component
  has to remember them; the touch minimum is the `touch-target` utility.

## Related

- [`docs/design-system.md`](../../docs/design-system.md)
- [`docs/localization.md`](../../docs/localization.md)
- [`docs/form-spec.md`](../../docs/form-spec.md)
- [ADR-0006](../../docs/decisions/ADR-0006-theme-engine.md) — why Tailwind's standalone CLI
- [ADR-0023](../../docs/decisions/ADR-0023-pinned-and-vendored-web-assets.md) — why the binary, fonts and logo are pinned and committed
- [ADR-0024](../../docs/decisions/ADR-0024-dark-mode-is-a-token-redefinition.md) — why there is no `dark:` variant
