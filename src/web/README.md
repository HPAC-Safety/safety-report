# Web

Static UI scaffold for the public report form and the authenticated review route.
It uses plain HTML and JavaScript with one generated Tailwind stylesheet; there
is no SPA framework or runtime package installation.

```text
public/   incident form
admin/    review UI
shared/   shared browser code
styles/   Tailwind source and local preview
assets/   self-hosted logo and fonts
```

Build CSS with `./tools/build-css.sh`. `site.css` is generated and untracked.
The eventual form must request current questions from the API, render their
English or French text, and require only publication consent. The admin UI is a
route on the same static site; the API remains its security boundary.
