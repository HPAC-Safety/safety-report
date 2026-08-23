# Working with coding agents

[`AGENTS.md`](../AGENTS.md) is the only always-loaded repository instruction;
the tool-specific instruction paths are symlinks to it. The product-design
authority is [`spec/README.md`](../spec/README.md).

## Start

1. Run `./init-dev.sh` or `./init-dev.sh --check`.
2. Read `AGENTS.md`, the affected `/spec` pages, and the focused issue.
3. Load only the project skills relevant to the task.
4. Work from current `main` on `issue-<number>/<short-description>`.

Project-owned skill sources live under `skills/`. `skillfile install` generates
tool-specific copies under `.claude/`; never edit or commit those copies. Keep
local skills concise and HPAC-specific. Search before adding generic guidance,
and do not install a skill whose architecture conflicts with `/spec`.

Runtime AI instructions are not coding-agent skills. The one current prompt
lives under `src/HpacSafety.Worker/Prompts/` and is deployed with the Worker.

## Generated files

| Output | Owning command |
|---|---|
| `.claude/skills/` | `skillfile install` |
| `Skillfile.lock` | `skillfile add`, `skillfile remove`, or `skillfile upgrade`; then `skillfile install` |
| `docs/form-spec.md` | `tools/extract-typeform.py` |
| `locales/fr-CA.json`, `locales/fr-CA.meta.json` | `tools/translate-locale.mjs` |
| `src/web/styles/site.css` | `tools/build-css.sh` |

Question text is not generated from locale catalogues: every database question
revision is manually authored in English and French.

## Finish

Run relevant tests and validation, inspect the diff, push the branch, and open a
PR containing `Closes #<number>`. Keep working until required checks are green.
See [`deliver-hpac-change`](../skills/deliver-hpac-change/SKILL.md).
