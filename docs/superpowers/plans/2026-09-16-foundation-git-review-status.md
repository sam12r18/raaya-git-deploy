# Foundation & Git Review — Verification Status

This file records verification evidence for `docs/superpowers/plans/2026-09-14-foundation-git-review.md`. A capability is only marked verified when the referenced automated check actually completed successfully. Manual desktop acceptance remains separate from CI evidence.

## Current checkpoint

- Branch: `feat/foundation-git-review`
- Current HEAD: `a68956371a467b0d421839eb5f0c0c6ff546f099` (`fix: clear stale repository state before reload`)
- Previous documented checkpoint: `3dc8f7a021e264fa1f65dfdca24e3715ef26a63f`
- CI status for current HEAD: **not verified** in this execution; GitHub returned no workflow run or combined status for `a689563`.
- Scope remains Foundation & Git Review only. Terminal, deployment, remote credentials, and SFTP are intentionally out of scope.

## Automated verification

The previous documented checkpoint `3dc8f7a` had CI run #89 marked **success** for the repository-picker single-flight fix. Subsequent commits added regression coverage and fixes for stale diff, comparison, and repository state, but this execution did not find a completed CI result for the current HEAD `a689563`; those changes must therefore remain **unverified** until a real workflow result is available.

Known regression coverage present in the branch includes:

- repository folder selection delegates a selected path to the workspace;
- cancelling repository selection does not invoke Git;
- picker failures are surfaced through workspace diagnostics;
- stale picker diagnostics are cleared before a retry;
- concurrent Open Repository attempts are single-flight and do not open duplicate pickers;
- stale diff preview is cleared before a reload;
- stale comparison state is cleared before a reload;
- stale repository state is cleared before a reload;
- existing Core/Infrastructure/Presentation test suites are configured in `.github/workflows/ci.yml`.

No item above is marked as passing for the current HEAD without completed CI evidence.

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

1. Re-check GitHub Actions for `a689563` and record the exact completed run if one becomes available.
2. If the current commit has no workflow execution, do not claim the branch is green; investigate the workflow trigger/configuration independently.
3. Preserve the picker diagnostics and single-flight behavior while waiting for real Windows acceptance evidence.
4. If desktop acceptance exposes a failure, reproduce it with the smallest automated regression possible before changing production code.
5. If desktop acceptance passes, close Task 8 acceptance and only then move to the separate follow-on plan.
