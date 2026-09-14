# Raaya Git Deploy — Implementation Sequence

The architecture is intentionally capability-oriented rather than constrained by fixed V1/V2/V3 product buckets.

Implementation is still split into independently reviewable slices so each subsystem can be tested and accepted before the next one depends on it.

## Slice A — Foundation & Git Review

**Detailed plan:** `docs/superpowers/plans/2026-09-14-foundation-git-review.md`

Delivers:

- solution/project boundaries,
- real `git.exe` process execution,
- repository context,
- machine-readable Working Tree changes,
- Changes Since Commit/Branch,
- first diff inspection,
- independent review and deploy-selection state,
- professional WinUI 3 repository workspace.

This is the first executable product slice.

## Slice B — Integrated Terminal & Saved Commands

Delivers:

- ConPTY session abstraction,
- repository-aware PowerShell terminal,
- terminal lifecycle/cancellation,
- terminal panel inside the workspace,
- saved commands,
- reusable build/test commands,
- explicit user-controlled command execution.

## Slice C — Deployment Planning & SFTP

Delivers:

- Add File,
- Add Folder,
- Deployment Queue,
- Local → Remote mapping,
- remote-root containment,
- server profiles,
- DPAPI-protected credentials,
- SSH host-key trust,
- Dry Run,
- SFTP upload,
- explicit delete confirmation,
- upload-before-delete safety.

## Slice D — Deployment History & Product Hardening

Delivers:

- Deployment Snapshot persistence,
- structured logs,
- history UI,
- cancellation/recovery UX,
- “Changes Since Last Deploy” groundwork,
- installer/package strategy,
- contributor quality gates,
- end-to-end hardening.

## Why the Work Is Split

These are implementation slices, not product-version limits.

A public contributor may propose a capability at any time. If a change affects an architectural baseline, it should include or update an ADR. If it is independently implementable, it can get its own implementation plan and does not need to wait for an arbitrary future “version”.

## Execution Rule

Each detailed implementation plan should:

1. reference `docs/architecture/PRODUCT_ARCHITECTURE.md`,
2. preserve architectural boundaries,
3. use test-first steps for domain logic,
4. end in a runnable/testable increment,
5. commit frequently,
6. update docs when architecture or behavior changes.
