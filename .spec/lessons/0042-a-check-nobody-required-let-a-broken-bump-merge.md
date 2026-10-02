---
title: A check nobody required let a broken bump merge
description: Renovate's ESLint 10 bump merged with lint failing, because lint ran on every pull request but was not a required check, and npm ci then failed on main and every branch rebased onto it.
type: lesson
date: 2026-10-02
issue: 788
status: accepted
---

# Lesson 0042 — A check nobody required let a broken bump merge

## Symptom

- [#784](https://github.com/HPAC-Safety/safety-report/pull/784) moved `eslint`
  and `@eslint/js` to `^10` in `package.json` and merged. `npm ci` then failed
  on `main`, and `lint` went red on every branch rebased onto it, #780 included
  ([#785](https://github.com/HPAC-Safety/safety-report/issues/785)).

## Root cause

- Renovate could not update `package-lock.json`: `eslint-plugin-jsx-a11y`
  6.10.2, the latest, accepts ESLint up to 9. The pull request carried only the
  manifest, and `lint` failed on it.
- `lint` was added to `ci.yml` after the main ruleset's required checks were
  set, and nobody added it there. A failing check that is not required does not
  hold a pull request, so the bump merged.

## Spec delta

None upstream: this is CI, not a product claim. `lint` joins the main ruleset's
required status checks; #786 reverted the bump.

## Scenario

None. This is a process lesson, and no scenario can prove it.

## Skill

[`deliver-hpac-change`](../../skills/deliver-hpac-change/SKILL.md) gains
"Required checks": a new job that can fail `main` is added to the ruleset's
required checks in the same pull request that adds it.
