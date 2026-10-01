---
title: An agent session link never reaches the public history
description: A commit-msg hook and a Linked issue workflow job refuse any commit message or pull-request body carrying a coding agent's session link, matched against a collection of per-agent link shapes.
type: adr
status: accepted
date: 2026-09-23
decision-makers: Chase Florell
keywords: session link, commit message, pull request body, public repository, commit-msg hook, coding agents
---

# ADR-0107 — An agent session link never reaches the public history

**Status:** Accepted.

## Context

This repository is public. The body of a pull request becomes the squash
commit message (`squash_merge_commit_message=PR_BODY`), so whatever the body
says lands in `main`'s history permanently.

Coding agents add a link to their own session by default. For example, Claude
Code ends a commit with `Claude-Session: https://claude.ai/code/session_…` and
a pull-request body with the bare link. That link opens a transcript that can
carry what the public history must not. The owner forbids these links, but a
rule written only in an agent's instructions holds only while the agent reads
and follows it
([lesson 0001](../lessons/0001-a-guard-that-lives-only-in-ci-is-not-a-guard.md)).

Claude is the only agent contributing today. Others, such as Codex or Gemini,
may follow, each with its own link shape.

## Decision

**A commit message or pull-request body carrying an agent session link is
refused.** The refusal runs where the text is written:

- **The `commit-msg` hook** (`.githooks/commit-msg`, installed by
  `init-dev.sh`) refuses the commit before it exists.
- **The `no-session-link` job in `linked-issue.yml`** refuses the
  pull-request body. That workflow already re-runs on `edited`, so fixing the
  body clears the check without a rebuild. The job has no bot exemption,
  because no author may publish a session link.

Both run `tools/agent-session-links.mjs`, whose `SESSION_LINKS` collection
holds one entry per agent: a name and the URL pattern its sessions use. Today
it holds only Claude's (`claude.ai/code/session_…`). Supporting another agent
is one more entry and one more test, with the link shape that agent actually
emits. Nobody guesses a shape in advance.

## Alternatives considered

- **Instructions only**, in `AGENTS.md` and the user's own agent settings.
  This is already in place, and the links still get written, because an
  agent's default attribution runs unless something refuses it.
- **Strip the link automatically.** Rejected: rewriting what an author wrote
  hides the mistake, and a squash commit message cannot be rewritten after the
  merge.
- **One pattern matching every agent.** Rejected: each agent's link has its
  own shape, and a broad pattern would refuse legitimate links, such as a
  `claude.ai` artifact link.

## Consequences

- A commit or pull request carrying a listed session link cannot land.
- A developer who has not run `./init-dev.sh` since this change gets the
  refusal from CI instead of from the hook.
- A new agent's link shape is not refused until someone adds it to the
  collection.
