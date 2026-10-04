---
name: write-agent-instructions
description: How to write and maintain agent instructions — the agent-file shape, style rules, the generic, split, and repository-specific kinds, and the editing procedure that loses no rule. Use when adding, changing, or auditing the agent instructions, a skill, or a role agent.
---

# Write agent instructions

**Project rules.** A project may extend this skill with a companion skill that
names it; its agent instructions (`AGENTS.md`) list it. Read both. The
companion holds the project's paths, files, and checks, and wins where they
differ.

## Read first

- The file, and every file that links to or restates it (`grep -rn` the
  rule's key phrase across the agent instructions, `skills/`, and `agents/`).
- The project's record of what the always-loaded instructions own versus a
  skill.
- `deliver-change` "Lessons": where a lesson's general rule lands.

## Style rules

The one home for how an instruction file is written; others link here.

- **Direct.** Imperative. A one-line *why* only where the rule surprises.
- **Bullets over prose.** Prose only for one idea that needs its reasoning.
- **Headed sections.** One concept per `##`/`###`.
- **No fluff.** No preamble, restatement, hedging, or origin story; link the
  decision record or lesson.
- **Say it once.** One home per rule; elsewhere links. The always-loaded
  instructions: what every task needs (invariants, specification authority,
  delivery basics, skill table). A skill: its topic's detail. An agent: its
  role.
- **Keep the reference.** A decision, lesson, or claim link stays beside its
  rule — in the project skill when the rule is generic.
- **Keep stable handles.** Invariant numbers, skill section names, and
  numbered steps are cited by code and lessons. Don't renumber or rename; if
  one must change, update every citation in the same change.

## An agent file

- **Frontmatter**: `name` (the role, never changed: it routes), `description`,
  `model`, `effort`, and where needed `tools`, `isolation`, and `skills`.
  - The description opens with the team role, then the persona name in
    parentheses: "The team's security engineer (Kyle). …". It then says what
    the agent does and when to pick it over its neighbours.
  - `skills` lists generic skills only. A role that edits files preloads
    `deliver-change`; a reviewer does not.
  - The agent declares its model and effort; the project skill says which and
    why.
- **Body**: exactly these, in this order.
  - `# <Name> — <role>`;
  - `## Who I am`: two to four lines of personality, voice, and what I care
    about;
  - `## What I do`: the mission, what I own, and for a reviewer the lens (its
    "Look for" list is the what);
  - `## What I leave to others`: every refusal, and other roles' work.
- **A refusal stays in the agent file.** A skill constrains nothing; a role is
  defined by what it refuses.
- **The how lives in skills**: reading lists, procedures, output formats, and
  git mechanics. The agent file says who and what.
- **A persona never changes a rule.** Voice follows `agent-persona`.

## Generic and project files

- **Generic** — transfers to any project: names no project, product, domain
  term, repository-unique path, or decision, lesson, or claim number.
- **Split** — a generic skill plus a small project skill that names it, keeps
  its section names, and holds only repository-specific rules. The generic
  one points to the project one; the always-loaded instructions list both.
- **Repository-specific** — its subject is one product. It stays as is.

Make a file generic only where it genuinely is. A rule naming one repository's
tool, path, or decision moves to the project skill, never out of existence.

## How you edit

1. **List before you cut**: every rule, constraint, command, path, and
   decision, lesson, or claim reference.
2. **Find duplicates** across the other instruction files; one home, links
   elsewhere.
3. **Rewrite** to the style rules.
4. **Diff the checklist**: each item is in the file, its project skill, or one
   link away. Put the result in the pull-request body.
5. **Verify**: the frontmatter and generic-file checks pass; a skill's
   `description` triggers on the same work (tighten wording, never narrow
   scope); every relative link resolves; a new skill or agent has its manifest
   entry and the install runs clean.
