---
name: build-hpac-web-ui
description: Build or review HPAC safety-report's static web UI using its localization, Tailwind theme, dark-mode, asset, and privacy rules. Use when changing files under src/web, HTML, browser JavaScript, CSS, Tailwind tokens, fonts, images, accessibility labels, or user-facing UI copy.
---

# Build the static UI

Read `src/web/README.md`, `docs/design-system.md`, and
[`localize-hpac-app`](../localize-hpac-app/SKILL.md) before editing the web
surface.

- Use static HTML and JavaScript. Do not add an SPA framework or bundler.
- Put every user-facing string, including accessibility text, behind a locale
  key. `src/web/styles/theme-preview.html` alone is exempt because its text is
  developer-facing token and font names.
- Build Tailwind v4 with the standalone CLI and the `@theme` tokens in
  `src/web/styles/tailwind.css`. Never add raw hex colours to markup.
- Implement dark mode by redefining tokens in both dark blocks. Use semantic
  utilities such as `bg-surface`; never use a `dark:` variant in markup.
- Add every new colour to `@theme` and both dark token blocks.
- Make no third-party page-load requests from `src/web`: no CDN fonts, scripts,
  or remote images. Commit assets and their licences under `src/web/assets`.
- Regenerate `src/web/styles/site.css` with `tools/build-css.sh`; never edit the
  generated stylesheet.

Run the web checks and exercise the user journey in English and French.
