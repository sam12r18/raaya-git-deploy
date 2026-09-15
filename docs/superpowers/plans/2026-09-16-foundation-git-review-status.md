# Foundation & Git Review — Verification Status

This file records verification evidence for `docs/superpowers/plans/2026-09-14-foundation-git-review.md`. A capability is only marked verified when the referenced automated check actually completed successfully. Manual desktop acceptance remains separate from CI evidence.

## Current checkpoint

- Branch: `feat/foundation-git-review`
- Verified code checkpoint before this status commit: `3dc8f7a021e264fa1f65dfdca24e3715ef26a63f`
- Latest completed CI at that checkpoint: run #89 — **success**
- Scope remains Foundation & Git Review only. Terminal, deployment, remote credentials, and SFTP are intentionally out of scope.

## Automated verification

CI run #89 completed successfully for commit `3dc8f7a` after the repository-picker single-flight fix. The workflow includes restore/build and the Core, Infrastructure, and Presentation test suites configured by `.github/workflows/ci.yml`.

The following behavior therefore has automated regression coverage at the verified checkpoint:

- repository folder selection delegates a selected path to the workspace;
- cancelling repository selection does not invoke Git;
- picker failures are surfaced through workspace diagnostics;
- stale picker diagnostics are cleared before a retry;
- concurrent Open Repository attempts are single-flight and do not open duplicate pickers;
- existing Core/Infrastructure/Presentation tests continue to pass under CI.

## Not manually verified

The following items MUST NOT be reported as passing until exercised on a real supported Windows desktop build:

- WinUI FolderPicker visibly opens from the Open Repository button;
- selecting a real repository displays the expected root, branch, and HEAD in the live UI;
- tracked and untracked changes appear correctly in the live change list;
- review state and deployment selection can be changed independently through the live controls;
- diff selection renders correctly in the live inspector;
- `HEAD~1` comparison works through the live controls;
- the UI remains responsive during real Git execution.

These correspond to the manual acceptance portion of Task 8. CI success is not a substitute for this desktop acceptance.

## Next execution

1. Check CI for this documentation checkpoint before claiming it is green.
2. Preserve the picker diagnostics and single-flight behavior while waiting for real Windows acceptance evidence.
3. If desktop acceptance exposes a failure, reproduce it with the smallest automated regression possible before changing production code.
4. If desktop acceptance passes, close Task 8 acceptance and only then move to the separate follow-on plan.
