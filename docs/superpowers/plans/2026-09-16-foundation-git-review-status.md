# Foundation & Git Review — Verification Status

This file records verification evidence for `docs/superpowers/plans/2026-09-14-foundation-git-review.md`. A capability is only marked verified when the referenced automated check actually completed successfully. Manual desktop acceptance remains separate from CI evidence.

## Current checkpoint

- Branch: `feat/foundation-git-review`
- Verified production checkpoint: `a68956371a467b0d421839eb5f0c0c6ff546f099` (`fix: clear stale repository state before reload`)
- CI run #96 for `a689563`: **success**.
- Documentation checkpoint before this update: `4f9ec28a9719fdec8c482bf269325e4e53b83856` (`docs: refresh foundation review verification checkpoint`)
- CI run #97 for `4f9ec28`: **success**.
- Scope remains Foundation & Git Review only. Terminal, deployment, remote credentials, and SFTP are intentionally out of scope.

## Automated verification

GitHub Actions run #96 completed successfully for the current production checkpoint `a689563`. Run #97 then completed successfully for the documentation-only checkpoint `4f9ec28`. The workflow includes restore/build and the Core, Infrastructure, and Presentation test suites configured by `.github/workflows/ci.yml`.

Regression coverage present in the verified branch includes:

- repository folder selection delegates a selected path to the workspace;
- cancelling repository selection does not invoke Git;
- picker failures are surfaced through workspace diagnostics;
- stale picker diagnostics are cleared before a retry;
- concurrent Open Repository attempts are single-flight and do not open duplicate pickers;
- stale diff preview is cleared before a reload;
- stale comparison state is cleared before a reload;
- stale repository state is cleared before a reload;
- existing Core/Infrastructure/Presentation test suites completed successfully in CI for the referenced checkpoints.

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

1. Check CI for this status-document update before marking its HEAD green.
2. Preserve the verified Git Review behavior while waiting for real Windows acceptance evidence.
3. If desktop acceptance exposes a failure, reproduce it with the smallest automated regression possible before changing production code.
4. If desktop acceptance passes, close Task 8 acceptance and only then move to the separate follow-on plan.
