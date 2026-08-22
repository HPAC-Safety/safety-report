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
| `styles/` | `tailwind.css` (source, with `@theme` tokens) → `site.css` (generated) |
| `assets/` | Logo, favicon, self-hosted Poppins and Aleo |

## Why plain HTML

A pilot fills this in after a crash, often on a phone, often on a bad connection
at a landing zone. Every kilobyte of framework is a kilobyte between them and
filing the report. There is no client state worth a framework — it is a form and
a list.

Fonts are self-hosted rather than loaded from Google, so filing a report makes
no third-party request.

## Building

```bash
./tools/build-css.sh          # downloads the Tailwind standalone binary, emits styles/site.css
```

`styles/site.css` is generated and gitignored. There is nothing else to build —
the HTML and JS ship as written.

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

**Candidates:** S3 + CloudFront (if AWS is confirmed), Cloudflare Pages, Azure
Static Web Apps, GitHub Pages.

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
- **No raw hex in markup.** Use the `@theme` tokens.
- Real focus rings, 44px touch targets, WCAG AA contrast.

## Related

- [`docs/design-system.md`](../../docs/design-system.md)
- [`docs/localization.md`](../../docs/localization.md)
- [`docs/form-spec.md`](../../docs/form-spec.md)
