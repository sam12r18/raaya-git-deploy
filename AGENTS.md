# Raaya Git Deploy — Agent Development Guide

This file defines the working contract for AI-assisted development of Raaya Git Deploy. It is guidance for development agents; it is not a runtime feature of the application.

## Goals

- Keep development context consistent across long-running sessions.
- Reduce repeated context processing by keeping a stable prompt prefix.
- Make work on multiple feature branches safe and explicit.
- Keep build/test/diff/task state dynamic and late in the context.
- Avoid unnecessary rewrites of stable instructions.

## Repository and branch discipline

Development is intentionally happening on multiple branches. Never assume `main` is the only active line of work.

Known active development branches include:

- `main` — integration/stable baseline.
- `feat/foundation-git-review` — foundation and Git review work.
- `feat/git-host-auto-deploy` — Git host / automatic deployment work.

Before changing code:

1. Inspect the current branch and HEAD.
2. Inspect active branches relevant to the requested task.
3. Compare with `main` when necessary to identify divergence.
4. Do not move unrelated work between branches merely to simplify agent context.
5. Prefer an isolated feature/chore branch for cross-cutting development-process changes.
6. Before merging or cherry-picking, check for overlapping changes and preserve branch-specific work.

## Cache-friendly context layout

When the AI/API environment supports prompt caching, construct development context in this order.

### 1. Stable prefix

Keep this section byte-for-byte stable whenever practical:

- system/development rules;
- this `AGENTS.md` contract;
- architecture and coding conventions;
- solution/project structure that has not changed;
- UI/UX and component conventions;
- security and deployment rules;
- tool definitions and schemas;
- repository-wide constraints.

Do not inject timestamps, current branch names, diffs, test output, transient status, or per-task commentary into the stable prefix.

### 2. Semi-dynamic project context

Place relatively slow-changing information after the stable prefix:

- roadmap and implementation sequence;
- current milestone;
- architectural decisions relevant to the current area;
- branch purpose and long-lived branch-specific constraints;
- accepted design decisions.

Update this section only when its source actually changes.

### 3. Dynamic task context

Append volatile information last:

- current branch and HEAD SHA;
- current user request;
- `git status` and relevant `git diff`;
- recent commits needed for the task;
- build/test output;
- runtime errors and logs;
- current implementation ledger/progress;
- immediate next action.

Prefer appending new task state instead of rewriting stable context.

## Prompt caching rules

- Preserve the longest possible common prefix between consecutive development turns.
- Do not reorder stable instructions or tool definitions without a functional reason.
- Do not regenerate stable repository summaries every turn.
- Reference canonical repository documents instead of producing slightly different copies of the same guidance.
- Keep volatile data out of the beginning of the prompt/context.
- If explicit cache breakpoints are available, place the primary breakpoint after the stable prefix; additional breakpoints may follow large semi-dynamic reference blocks when useful.
- If tool availability can be constrained without changing tool schemas, prefer a stable tool definition set plus per-task allowed-tool selection.
- Changing reasoning effort for a task must not cause stable project context to be rewritten unnecessarily.
- Use cache diagnostics/metrics when the execution environment exposes them; optimize based on measured cache misses rather than assumptions.

## Development cycle

For each development cycle:

1. Resolve the intended branch before editing.
2. Read only the repository context required for the task, while retaining the canonical stable context.
3. Inspect the relevant diff/history before changing overlapping code.
4. Implement the smallest coherent change.
5. Build and run the relevant tests.
6. Review the resulting diff from correctness and UI/UX perspectives when applicable.
7. Record remaining work and newly discovered issues without rewriting stable project guidance.
8. Keep commits cohesive and branch-specific.

## Current product priorities

The development process must not let context/caching optimization displace product work. Current priorities include:

- FTP support for cPanel-oriented deployment in addition to SSH/SFTP flows;
- an IDE-like integrated terminal;
- a richer commit/history browser inspired by professional IDEs, including author/date/path and branch hierarchy context;
- continued component-oriented UI/UX cleanup and consistency;
- safe deployment queue and Local → Remote preview behavior;
- continued coordination between desktop and planned Android work.

## Canonical project documents

Use these as source documents rather than duplicating their contents into changing prompt text:

- `README.md`
- `README.fa.md`
- `CONTRIBUTING.md`
- `docs/ROADMAP.md`
- `docs/IMPLEMENTATION_SEQUENCE.md`
- architecture and ADR documents under `docs/`

If this guide conflicts with a newer approved architecture/ADR or an explicit user instruction, the newer approved decision wins. Update the canonical documentation when the conflict represents a lasting project decision.