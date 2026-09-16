# Foundation & Git Review — Verification Status

This file records verification evidence for `docs/superpowers/plans/2026-09-14-foundation-git-review.md`. A capability is only marked verified when the referenced automated check actually completed successfully. Manual desktop acceptance remains separate from CI evidence.

## Current checkpoint

- Branch: `feat/foundation-git-review`
- Verified production checkpoint: `2c729ba8c46a7698fe7742d3207f8b0aa568c946` (`fix: resolve comparison refs before git diff`)
- CI run #102 for `2c729ba`: **success**.
- The preceding test-only checkpoint `c7625ca084c5afa4c33fd7f48160be45e4cfd7f0` had CI run #101 cancelled and is not recorded as passing.
- Earlier attempt `748d5eb589c6dade118bf2883d804731e036cc7e` failed CI because placing `--` before a revision changed Git semantics by treating the revision as a pathspec. That implementation is superseded by `2c729ba`.
- Scope remains Foundation & Git Review only. Terminal, deployment, remote credentials, and SFTP are intentionally out of scope.

## Automated verification

GitHub Actions run #102 completed successfully for production checkpoint `2c729ba`. The workflow includes restore/build and the Core, Infrastructure, and Presentation test suites configured by `.github/workflows/ci.yml`.

Regression coverage present in the verified branch includes:

- repository folder selection delegates a selected path to the workspace;
- cancelling repository selection does not invoke Git;
- picker failures are surfaced through workspace diagnostics;
- stale picker diagnostics are cleared before a retry;
- concurrent Open Repository attempts are single-flight and do not open duplicate pickers;
- stale diff preview is cleared before a reload;
- stale comparison state is cleared before a reload;
- stale repository state is cleared before a reload;
- user-controlled comparison refs are resolved with `git rev-parse --verify --end-of-options <ref>^{commit}` before use by `git diff`;
- both comparison change loading and base-ref diff loading use the resolved commit rather than passing the user-controlled ref directly to `git diff`;
- existing Core/Infrastructure/Presentation test suites completed successfully in CI for production checkpoint `2c729ba`.

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
3. Continue reviewing this slice for independently reproducible Git/Presentation regressions; use RED → minimal fix → GREEN when one is found.
4. If desktop acceptance exposes a failure, reproduce it with the smallest automated regression possible before changing production code.
5. If desktop acceptance passes, close Task 8 acceptance and only then move to the separate follow-on plan.
