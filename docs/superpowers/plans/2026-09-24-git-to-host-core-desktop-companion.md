# Git-to-Host Core, Desktop & Companion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn Raaya Git Deploy from a manual file-transfer workflow into a Git-aware deployment workflow that automatically derives the pending deployment range from the last successful deployed commit, builds a reviewable queue, performs a target-SHA-aware Dry Run, deploys safely, records the new baseline, and exposes the same safe intent to the Mobile companion.

**Architecture:** Extend the existing Git, Deployment, History and Companion contracts rather than creating a second deployment stack. Desktop owns local Git mutation and FTP/FTPS/SFTP execution; shared Core owns transport-neutral pending-range, queue and history semantics; Mobile consumes agent-issued repository/profile/plan identifiers and never receives host credentials. Android application-host/Keystore wiring is intentionally a separate follow-up plan because it is an independent platform subsystem.

**Tech Stack:** .NET 10 / C# 14, WinUI 3, git.exe via existing process runner, existing Core/Infrastructure/Presentation/App layers, xUnit, System.Text.Json, existing SFTP + FluentFTP transport adapters.

**Spec:** `docs/superpowers/specs/2026-09-24-git-to-host-deployment-workflow-design.md`

## Global Constraints

- Primary Desktop flow: **Open Repository → Update/Pull → Detect Pending Deployment → Review → Auto Queue → Dry Run → Deploy → Record Successful HEAD → History**.
- Canonical deployment range is `last_successfully_deployed_head(repository, profile)..current_local_head`, not merely the last Pull range.
- Multiple Pulls before deployment must accumulate; failed/partial/cancelled deployment must not advance the successful baseline.
- Automated Pull requires a clean working tree and fast-forward-only behavior; divergence/conflict stops without implicit merge.
- FTP/FTPS/SFTP credentials stay inside Desktop/agent Infrastructure; transport-specific details must not leak into shared deployment/history models.
- Queue provenance must distinguish Git-detected, generated/rule-based and manual entries.
- Delete/rename-derived remote deletes are destructive, visible in Dry Run and explicitly confirmed.
- Mobile uses agent-issued repository/profile/plan identifiers, HTTPS authorization and explicit confirmation; it must not accept or store raw SSH/FTP/FTPS/Git credentials.
- Do not modify or merge `feat/git-host-auto-deploy` blindly; reconcile only reviewed shared contracts.
- Existing JSON history must remain readable after schema expansion.

## Review Focus

1. **Legacy history JSON with no Git metadata** — loads as a legacy entry instead of crashing; it cannot become a Git baseline until repository/profile metadata exists.
2. **Repository path casing / normalization on Windows** — the same repository opened with equivalent path casing still resolves the correct profile baseline.
3. **HEAD changes after Dry Run** — execution is rejected or remains explicitly pinned; it must never silently advance a newer SHA than the reviewed target.
4. **Rename/delete with upload failure** — delete phase remains blocked by the existing upload-before-delete safety rule and baseline does not advance.
5. **Mobile attempts to invent arbitrary paths** — the new plan-based companion API rejects client-supplied filesystem paths and only accepts authorized plan/item IDs.

---

### Task 1: Add Git-aware deployment history and baseline semantics

**Files:**
- Modify: `src/RaayaGitDeploy.Core/Deployment/IDeploymentHistoryStore.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/DeploymentBaselineService.cs`
- Create: `tests/RaayaGitDeploy.Core.Tests/Deployment/DeploymentBaselineServiceTests.cs`
- Modify existing history-construction tests under `tests/RaayaGitDeploy.Core.Tests/Deployment/` only where constructor compatibility needs verification.

**Interfaces:**
- Consumes: existing `IDeploymentHistoryStore.LoadAsync` and `DeploymentHistoryEntry`.
- Produces: expanded backward-compatible `DeploymentHistoryEntry` and `DeploymentBaselineService.GetLastSuccessfulAsync(string repositoryPath, string serverProfileId, CancellationToken)`.

- [ ] **Step 1: Write failing baseline tests**

```csharp
[Fact]
public async Task Returns_latest_successful_entry_for_repository_and_profile()
{
    var store = new FakeHistoryStore(
    [
        Entry("1", succeeded: true,  repo: @"C:\work\app", profile: "prod", toHead: "aaa", started: "2026-09-24T10:00:00Z"),
        Entry("2", succeeded: false, repo: @"C:\work\app", profile: "prod", toHead: "bbb", started: "2026-09-24T11:00:00Z"),
        Entry("3", succeeded: true,  repo: @"c:\WORK\app", profile: "prod", toHead: "ccc", started: "2026-09-24T12:00:00Z")
    ]);

    var service = new DeploymentBaselineService(store);
    var baseline = await service.GetLastSuccessfulAsync(@"C:\work\app", "prod", CancellationToken.None);

    Assert.NotNull(baseline);
    Assert.Equal("ccc", baseline!.ToHead);
}

[Fact]
public async Task Ignores_failed_and_legacy_entries_as_successful_git_baselines()
{
    var store = new FakeHistoryStore(
    [
        new DeploymentHistoryEntry("legacy", DateTimeOffset.UtcNow, "prod", "Production", true, []),
        Entry("failed", false, @"C:\work\app", "prod", "bbb", "2026-09-24T12:00:00Z")
    ]);

    var service = new DeploymentBaselineService(store);
    Assert.Null(await service.GetLastSuccessfulAsync(@"C:\work\app", "prod", CancellationToken.None));
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

Run:

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --filter DeploymentBaselineServiceTests
```

Expected: FAIL because Git metadata and `DeploymentBaselineService` do not exist yet.

- [ ] **Step 3: Expand history without breaking old callers/JSON**

Use optional trailing metadata so current constructor calls and legacy JSON remain valid:

```csharp
public sealed record DeploymentHistoryEntry(
    string Id,
    DateTimeOffset StartedAt,
    string ServerProfileId,
    string ServerDisplayName,
    bool Succeeded,
    IReadOnlyList<DeploymentItemResult> Items,
    string? RepositoryPath = null,
    string? Branch = null,
    string? FromHead = null,
    string? ToHead = null,
    DateTimeOffset? FinishedAt = null);
```

Add a focused resolver:

```csharp
public sealed class DeploymentBaselineService(IDeploymentHistoryStore store)
{
    public async Task<DeploymentHistoryEntry?> GetLastSuccessfulAsync(
        string repositoryPath,
        string serverProfileId,
        CancellationToken cancellationToken)
    {
        var normalizedRepository = Path.GetFullPath(repositoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return (await store.LoadAsync(cancellationToken))
            .Where(entry => entry.Succeeded &&
                            !string.IsNullOrWhiteSpace(entry.RepositoryPath) &&
                            !string.IsNullOrWhiteSpace(entry.ToHead) &&
                            string.Equals(Path.GetFullPath(entry.RepositoryPath!).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), normalizedRepository, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(entry.ServerProfileId, serverProfileId, StringComparison.Ordinal))
            .OrderByDescending(entry => entry.FinishedAt ?? entry.StartedAt)
            .FirstOrDefault();
    }
}
```

- [ ] **Step 4: Run Core tests**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/RaayaGitDeploy.Core/Deployment tests/RaayaGitDeploy.Core.Tests/Deployment
git commit -m "feat(core): track deployed git baseline in history"
```

---

### Task 2: Make JSON history schema migration backward-compatible

**Files:**
- Modify: `src/RaayaGitDeploy.Infrastructure/Deployment/JsonDeploymentHistoryStore.cs`
- Create or modify: `tests/RaayaGitDeploy.Infrastructure.Tests/Deployment/JsonDeploymentHistoryStoreTests.cs`

**Interfaces:**
- Consumes: expanded `DeploymentHistoryEntry` from Task 1.
- Produces: persisted/readable Git metadata while preserving legacy entries.

- [ ] **Step 1: Add a legacy JSON read test**

```csharp
[Fact]
public async Task LoadAsync_reads_legacy_entry_without_git_metadata()
{
    await File.WriteAllTextAsync(_path, """
    [{
      "Id":"legacy-1",
      "StartedAt":"2026-09-20T10:00:00+00:00",
      "ServerProfileId":"prod",
      "ServerDisplayName":"Production",
      "Succeeded":true,
      "Items":[]
    }]
    """);

    var entries = await new JsonDeploymentHistoryStore(_path).LoadAsync(CancellationToken.None);

    Assert.Single(entries);
    Assert.Null(entries[0].RepositoryPath);
    Assert.Null(entries[0].ToHead);
}
```

Add a round-trip test asserting `RepositoryPath`, `Branch`, `FromHead`, `ToHead`, `FinishedAt` survive append/load.

- [ ] **Step 2: Run Infrastructure tests and verify the new assertions fail if store options need adjustment**

```powershell
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug --filter JsonDeploymentHistoryStoreTests
```

- [ ] **Step 3: Update store serialization only as needed**

Keep `System.Text.Json` tolerant of absent optional properties; do not introduce a destructive migration. Ensure null/legacy metadata is preserved and append still uses atomic temp-file replacement if the current store already does so.

- [ ] **Step 4: Run Infrastructure tests**

```powershell
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/RaayaGitDeploy.Infrastructure/Deployment/JsonDeploymentHistoryStore.cs tests/RaayaGitDeploy.Infrastructure.Tests/Deployment
git commit -m "feat(history): persist git deployment baseline metadata"
```

---

### Task 3: Add safe fast-forward-only Desktop Git Update/Pull

**Files:**
- Create: `src/RaayaGitDeploy.Core/Git/IGitUpdateService.cs`
- Create: `src/RaayaGitDeploy.Infrastructure/GitCli/GitUpdateService.cs`
- Create: `tests/RaayaGitDeploy.Infrastructure.Tests/GitCli/GitUpdateServiceTests.cs`

**Interfaces:**
- Produces:

```csharp
public sealed record GitUpdateResult(
    string RepositoryPath,
    string Branch,
    string OldHead,
    string NewHead,
    bool Changed);

public interface IGitUpdateService
{
    Task<GitUpdateResult> UpdateFastForwardOnlyAsync(string repositoryPath, CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write process-sequence tests**

Test three cases using the existing fake/stub `IGitProcessRunner` pattern in Infrastructure tests:

```csharp
[Fact]
public async Task Rejects_dirty_working_tree_before_fetch_or_pull() { /* arrange porcelain output, assert InvalidOperationException and no pull command */ }

[Fact]
public async Task Rejects_repository_without_upstream() { /* upstream rev-parse exits non-zero, assert actionable error */ }

[Fact]
public async Task Uses_fast_forward_only_and_returns_before_after_heads() { /* old abc, pull --ff-only, new def */ }
```

The successful test must assert the mutation command contains `pull --ff-only` and never plain `pull`.

- [ ] **Step 2: Run focused tests and verify failure**

```powershell
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug --filter GitUpdateServiceTests
```

- [ ] **Step 3: Implement safe update policy**

The service sequence is:

```text
git status --porcelain=v2 -z
→ require no records
git rev-parse --abbrev-ref HEAD
→ reject detached HEAD
git rev-parse --abbrev-ref --symbolic-full-name @{u}
→ require upstream
git rev-parse HEAD
→ OldHead
git pull --ff-only
→ reject non-zero exit; do not merge/rebase implicitly
git rev-parse HEAD
→ NewHead
```

Return `Changed = !StringComparer.Ordinal.Equals(OldHead, NewHead)`.

- [ ] **Step 4: Run all Infrastructure tests**

```powershell
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug
```

- [ ] **Step 5: Commit**

```bash
git add src/RaayaGitDeploy.Core/Git/IGitUpdateService.cs src/RaayaGitDeploy.Infrastructure/GitCli/GitUpdateService.cs tests/RaayaGitDeploy.Infrastructure.Tests/GitCli/GitUpdateServiceTests.cs
git commit -m "feat(git): add safe fast-forward repository update"
```

---

### Task 4: Add exact commit-range queries and pending deployment snapshot

**Files:**
- Modify: `src/RaayaGitDeploy.Core/Git/IGitRepositoryService.cs`
- Modify: `src/RaayaGitDeploy.Infrastructure/GitCli/GitRepositoryService.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/PendingDeploymentSnapshot.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/PendingDeploymentService.cs`
- Create: `tests/RaayaGitDeploy.Core.Tests/Deployment/PendingDeploymentServiceTests.cs`
- Modify/add Infrastructure Git tests for exact range parsing.

**Interfaces:**
- Add to `IGitRepositoryService`:

```csharp
Task<IReadOnlyList<GitCommitInfo>> GetCommitsBetweenAsync(
    string repositoryPath, string baseRef, string targetRef, CancellationToken cancellationToken);

Task<IReadOnlyList<GitChange>> GetChangesBetweenAsync(
    string repositoryPath, string baseRef, string targetRef, CancellationToken cancellationToken);
```

- Produce:

```csharp
public sealed record PendingDeploymentSnapshot(
    string RepositoryPath,
    string Branch,
    string? FromHead,
    string ToHead,
    IReadOnlyList<GitCommitInfo> Commits,
    IReadOnlyList<GitChange> Changes,
    bool RequiresBaseline);
```

- [ ] **Step 1: Add tests for multiple Pull accumulation and missing baseline**

```csharp
[Fact]
public async Task Builds_full_range_from_last_successful_deploy_to_current_head()
{
    // baseline A, current C; assert repository service queried A..C, not B..C.
}

[Fact]
public async Task Returns_baseline_required_when_profile_has_no_git_aware_success_history()
{
    // assert RequiresBaseline=true and no guessed FromHead.
}
```

- [ ] **Step 2: Run Core focused tests and verify failure**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --filter PendingDeploymentServiceTests
```

- [ ] **Step 3: Implement exact Git range parsing**

Use machine-readable commands in `GitRepositoryService`:

```text
git log --format=<existing NUL-safe commit format> baseRef..targetRef
git diff --name-status -z --find-renames baseRef targetRef
```

Reuse existing `GitCommitInfo`, `GitChange`, rename parsing and path safety conventions rather than parsing human-readable output.

- [ ] **Step 4: Implement `PendingDeploymentService`**

It loads repository context, resolves profile baseline through `DeploymentBaselineService`, then queries exact committed range. It must not include working-tree-only changes in a deployment range.

- [ ] **Step 5: Run Core + Infrastructure suites**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug
```

- [ ] **Step 6: Commit**

```bash
git add src/RaayaGitDeploy.Core/Git src/RaayaGitDeploy.Core/Deployment src/RaayaGitDeploy.Infrastructure/GitCli tests/RaayaGitDeploy.Core.Tests tests/RaayaGitDeploy.Infrastructure.Tests
git commit -m "feat(deploy): derive pending range from deployed head"
```

---

### Task 5: Convert Git delta into an auto-populated deployment queue with provenance

**Files:**
- Modify: `src/RaayaGitDeploy.Core/Deployment/DeploymentQueueItem.cs`
- Modify: `src/RaayaGitDeploy.Core/Deployment/DeploymentPlanner.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/PendingDeploymentQueueBuilder.cs`
- Create: `tests/RaayaGitDeploy.Core.Tests/Deployment/PendingDeploymentQueueBuilderTests.cs`
- Extend: `tests/RaayaGitDeploy.Core.Tests/Deployment/DeploymentPlannerTests.cs`

**Interfaces:**

```csharp
public enum DeploymentQueueSource
{
    GitSelection, // retain for compatibility
    GitDetected,
    GeneratedRule,
    ManualFile,
    ManualFolder
}

public enum DeploymentQueueAction
{
    Upload,
    Delete
}

public sealed record DeploymentQueueItem(
    string LocalPath,
    DeploymentQueueSource Source,
    string? RemotePath = null,
    DeploymentQueueAction Action = DeploymentQueueAction.Upload);
```

- [ ] **Step 1: Write mapping tests**

Assert:

```text
Added     → Upload new path / GitDetected
Modified  → Upload path / GitDetected
Deleted   → Delete old path / GitDetected
Renamed   → Upload new path + Delete OriginalPath / GitDetected
```

Add a deduplication test where the same normalized upload path appears as `GitDetected` and `GeneratedRule`; expected one upload operation with deterministic provenance precedence `Manual > GeneratedRule > GitDetected` only when an explicit higher-priority source exists.

- [ ] **Step 2: Add planner delete tests**

```csharp
[Fact]
public void Delete_item_does_not_require_local_file_to_exist_and_maps_inside_remote_root() { }

[Fact]
public void Delete_item_outside_repository_is_rejected() { }
```

- [ ] **Step 3: Run focused Core tests and verify failure**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --filter "PendingDeploymentQueueBuilderTests|DeploymentPlannerTests"
```

- [ ] **Step 4: Implement queue builder and queue-aware planner overload**

Add:

```csharp
public DeploymentPlan Plan(
    string repositoryRoot,
    string remoteRoot,
    IEnumerable<DeploymentQueueItem> items,
    bool dryRun = false)
```

For `Delete`, compute containment and remote mapping from the repository-relative path but do not require local existence. Keep current string-path overload delegating to upload queue items for backward compatibility.

- [ ] **Step 5: Run Core suite**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug
```

- [ ] **Step 6: Commit**

```bash
git add src/RaayaGitDeploy.Core/Deployment tests/RaayaGitDeploy.Core.Tests/Deployment
git commit -m "feat(deploy): auto-build queue from git delta"
```

---

### Task 6: Make Dry Run SHA-aware and protect the deployed baseline

**Files:**
- Modify: `src/RaayaGitDeploy.Core/Deployment/DeploymentPlan.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/DeploymentPlanContext.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/DeploymentRunCoordinator.cs`
- Create: `tests/RaayaGitDeploy.Core.Tests/Deployment/DeploymentRunCoordinatorTests.cs`

**Interfaces:**

```csharp
public sealed record DeploymentPlanContext(
    string RepositoryPath,
    string Branch,
    string? FromHead,
    string ToHead,
    string ServerProfileId);

public sealed record DeploymentPlan(
    IReadOnlyList<DeploymentOperation> Operations,
    bool IsDryRun,
    DeploymentPlanContext? Context = null);
```

`DeploymentRunCoordinator` owns the transition from reviewed preview to actual execution/history append.

- [ ] **Step 1: Write stale-preview and baseline tests**

```csharp
[Fact]
public async Task Rejects_execution_when_current_head_differs_from_reviewed_target() { /* plan C, current D */ }

[Fact]
public async Task Failed_result_is_recorded_but_does_not_create_successful_C_baseline() { }

[Fact]
public async Task Successful_result_records_exact_from_and_to_sha() { }
```

Also pin review-focus case: rename upload failure leaves delete blocked and overall result false, therefore no successful baseline advances.

- [ ] **Step 2: Run focused tests and verify failure**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --filter DeploymentRunCoordinatorTests
```

- [ ] **Step 3: Implement coordinator**

Before real execution, require `plan.Context != null`, load current repository context and compare `HeadSha` to `plan.Context.ToHead`. Append history for both success/failure, but only entries with `Succeeded=true` are considered by `DeploymentBaselineService`.

- [ ] **Step 4: Run Core tests**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug
```

- [ ] **Step 5: Commit**

```bash
git add src/RaayaGitDeploy.Core/Deployment tests/RaayaGitDeploy.Core.Tests/Deployment
git commit -m "feat(deploy): pin dry run and history to target head"
```

---

### Task 7: Add initial baseline onboarding for existing hosts

**Files:**
- Create: `src/RaayaGitDeploy.Core/Deployment/DeploymentBaselineRegistration.cs`
- Modify: `src/RaayaGitDeploy.Core/Deployment/DeploymentBaselineService.cs`
- Create: `tests/RaayaGitDeploy.Core.Tests/Deployment/DeploymentBaselineRegistrationTests.cs`
- Modify: `src/RaayaGitDeploy.Presentation/Deployment/DeploymentWorkspaceViewModel.cs`
- Extend Presentation tests for baseline-required state.

**Interfaces:**

```csharp
public sealed record DeploymentBaselineRegistration(
    string RepositoryPath,
    string Branch,
    string Head,
    string ServerProfileId,
    string ServerDisplayName);
```

- [ ] **Step 1: Test explicit registration and no-guess behavior**

Registration writes an auditable history entry with `FromHead == ToHead == selected baseline SHA`, empty item results, and success=true. No registration occurs automatically when history is missing.

- [ ] **Step 2: Run Core/Presentation focused tests and verify failure**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --filter DeploymentBaselineRegistrationTests
dotnet test tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj -c Debug --filter DeploymentWorkspace
```

- [ ] **Step 3: Implement baseline-required ViewModel state**

Expose clear state such as:

```csharp
public bool RequiresDeploymentBaseline { get; private set; }
public string? BaselineMessage { get; private set; }
```

The first supported action is `Mark current HEAD as deployed baseline`; choosing an arbitrary historical commit can be added in the next Desktop UI slice after the core registration contract exists.

- [ ] **Step 4: Run Core + Presentation tests**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug
dotnet test tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj -c Debug
```

- [ ] **Step 5: Commit**

```bash
git add src/RaayaGitDeploy.Core/Deployment src/RaayaGitDeploy.Presentation/Deployment tests/RaayaGitDeploy.Core.Tests tests/RaayaGitDeploy.Presentation.Tests
git commit -m "feat(deploy): add explicit deployed baseline onboarding"
```

---

### Task 8: Re-center Desktop workspace on Update → Pending → Dry Run → Deploy

**Files:**
- Modify: `src/RaayaGitDeploy.Presentation/Deployment/DeploymentWorkspaceViewModel.cs`
- Modify: `src/RaayaGitDeploy.Presentation/Deployment/DeploymentQueueViewModel.cs`
- Modify: `src/RaayaGitDeploy.Presentation/Deployment/DeploymentDryRunViewModel.cs`
- Modify: `src/RaayaGitDeploy.App/Views/RepositoryWorkspacePage.xaml`
- Create: `src/RaayaGitDeploy.App/Views/RepositoryWorkspacePage.DeploymentFlow.cs`
- Modify composition files under `src/RaayaGitDeploy.App/Bootstrap/` that currently construct Git/deployment services.
- Extend: `tests/RaayaGitDeploy.Presentation.Tests/` with workflow-state tests.

**Interfaces:**
- Consumes Tasks 3–7.
- Produces a primary user flow that does not require file-by-file filesystem lookup.

- [ ] **Step 1: Add ViewModel state-transition tests**

Pin these transitions:

```text
repo/profile selected + baseline A + HEAD C → Pending 2+ commits/files visible
Update succeeds B→C → pending recomputed from A→C
Prepare Deployment → queue auto-populated
Dry Run ready → Deploy enabled
HEAD changes after Dry Run → Deploy disabled / preview stale
successful Deploy → baseline C and pending count 0
```

- [ ] **Step 2: Run Presentation tests and verify failure**

```powershell
dotnet test tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj -c Debug
```

- [ ] **Step 3: Implement dominant workflow state in `DeploymentWorkspaceViewModel`**

Expose fields needed by XAML:

```csharp
public string? LastDeployedHead { get; private set; }
public string? CurrentHead { get; private set; }
public int PendingCommitCount { get; private set; }
public int PendingFileCount { get; private set; }
public bool CanUpdateRepository { get; private set; }
public bool CanPrepareDeployment { get; private set; }
public bool CanDeployReviewedPlan { get; private set; }
```

Keep Terminal, Commands, Servers, Changes and History available as secondary workspaces.

- [ ] **Step 4: Change the WinUI primary surface**

At the top of `RepositoryWorkspacePage.xaml`, add a compact deployment summary card with repository/branch, selected profile, current SHA, last deployed SHA and pending counts. Primary actions are `Update / Pull`, `Review Pending`, `Dry Run`, and `Deploy`.

Do not delete existing advanced views. Use the existing component/partial-class pattern and the new `RepositoryWorkspacePage.DeploymentFlow.cs` for event handlers so `RepositoryWorkspacePage.xaml.cs` does not grow further.

- [ ] **Step 5: Run Presentation tests + full build**

```powershell
dotnet test tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj -c Debug
dotnet build RaayaGitDeploy.slnx -c Debug
```

Expected: PASS on a Windows/.NET environment capable of building WinUI.

- [ ] **Step 6: Manual Windows acceptance**

Run:

```powershell
dotnet run --project src/RaayaGitDeploy.App/RaayaGitDeploy.App.csproj -c Debug --no-build
```

Verify with a disposable Git repository/host profile: no clipped primary action labels, pending counts update after Pull, queue appears without manually browsing each changed file, split/master-detail views remain resizable, destructive delete candidates are visually distinct, and stale preview disables Deploy.

- [ ] **Step 7: Commit**

```bash
git add src/RaayaGitDeploy.Presentation/Deployment src/RaayaGitDeploy.App/Views src/RaayaGitDeploy.App/Bootstrap tests/RaayaGitDeploy.Presentation.Tests
git commit -m "feat(desktop): center workspace on update and deploy"
```

---

### Task 9: Replace Mobile arbitrary-path deployment with agent-issued pending plans

**Files:**
- Modify: `src/RaayaGitDeploy.Core/Companion/CompanionContracts.cs`
- Modify: `src/RaayaGitDeploy.Android.Core/Api/CompanionDeploymentHttpClient.cs`
- Modify: `src/RaayaGitDeploy.Android.Core/Deployment/CompanionDeploymentWorkflow.cs`
- Extend: `tests/RaayaGitDeploy.Android.Core.Tests/` companion API/workflow tests.

**Interfaces:**

Add safe metadata contracts:

```csharp
public sealed record CompanionPendingItem(string Id, string Path, string Kind, bool Destructive);

public sealed record CompanionPendingPlan(
    string Id,
    string RepositoryId,
    string ProfileId,
    string? FromHead,
    string ToHead,
    IReadOnlyList<GitCommitInfo> Commits,
    IReadOnlyList<CompanionPendingItem> Items,
    DateTimeOffset CreatedAt);

public sealed record CompanionDryRunRequest(
    string PendingPlanId,
    IReadOnlyList<string> SelectedItemIds);
```

Add API operation:

```csharp
Task<CompanionPendingPlan> GetPendingPlanAsync(
    string repositoryId,
    string profileId,
    CancellationToken cancellationToken);
```

Evolve Dry Run to consume `CompanionDryRunRequest`; do not accept raw filesystem paths from Mobile.

- [ ] **Step 1: Write security-boundary tests**

Tests must prove:

```text
pending plan repository/profile must match selected scope
selected item IDs must exist in the authorized plan
empty selection is rejected
client cannot submit a raw /public_html or local filesystem path
preview returned for another repo/profile is rejected
```

- [ ] **Step 2: Run Android Core tests and verify failure**

```powershell
dotnet test tests/RaayaGitDeploy.Android.Core.Tests/RaayaGitDeploy.Android.Core.Tests.csproj -c Debug
```

- [ ] **Step 3: Implement plan-based workflow**

Add `PendingPlan` state to `CompanionDeploymentWorkflow`. Selection becomes item-ID based. Keep existing HTTPS/token/scope validation and offline/rate-limit activity states unchanged.

- [ ] **Step 4: Update HTTP client endpoints**

Use agent-controlled routes shaped like:

```text
GET  /api/companion/repositories/{repositoryId}/profiles/{profileId}/pending-plan
POST /api/companion/deployment-previews
POST /api/companion/deployments
```

Payload for preview contains plan ID + selected item IDs only. No SSH key, FTP password, Git credential or arbitrary remote path field is added.

- [ ] **Step 5: Run Android Core tests**

```powershell
dotnet test tests/RaayaGitDeploy.Android.Core.Tests/RaayaGitDeploy.Android.Core.Tests.csproj -c Debug
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/RaayaGitDeploy.Core/Companion src/RaayaGitDeploy.Android.Core tests/RaayaGitDeploy.Android.Core.Tests
git commit -m "feat(mobile): deploy from agent-issued pending plans"
```

---

### Task 10: Full regression verification and roadmap/checkpoint alignment

**Files:**
- Modify: `docs/IMPLEMENTATION_SEQUENCE.md`
- Modify: `docs/ROADMAP.md` only if implemented status differs from current Active Development text.
- Update the current status/checkpoint document under `docs/superpowers/plans/` rather than rewriting the historical design spec.

**Interfaces:** None; verification and documentation only after product code is green.

- [ ] **Step 1: Run the complete test/build matrix**

```powershell
dotnet restore RaayaGitDeploy.slnx
dotnet build RaayaGitDeploy.slnx -c Debug --no-restore
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --no-build
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug --no-build
dotnet test tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj -c Debug --no-build
dotnet test tests/RaayaGitDeploy.Android.Core.Tests/RaayaGitDeploy.Android.Core.Tests.csproj -c Debug --no-build
```

Only label these `TESTED-PASS` if each command actually exits successfully.

- [ ] **Step 2: Execute acceptance scenarios**

Desktop disposable-repo cases:

```text
A deployed → Pull to B → Pull to C → pending must be A..C
A deployed → Dry Run C → deployment failure → baseline stays A
A deployed → Dry Run C → full success → baseline becomes C
Dry Run C → local HEAD changes D → old preview cannot Deploy as current
rename + failed upload → delete blocked → baseline unchanged
```

Mobile contract cases:

```text
authorized pending plan → item selection → Dry Run → confirmed deployment
invented item ID/path → rejected
repo/profile mismatch → rejected
```

- [ ] **Step 3: Update implementation sequence**

Document the implemented order as Git baseline/history → safe Pull → pending range → auto queue → target-aware Dry Run → Desktop flow → companion plan contract. Keep Android platform host/Keystore explicitly identified as the next independent platform plan.

- [ ] **Step 4: Commit docs**

```bash
git add docs/IMPLEMENTATION_SEQUENCE.md docs/ROADMAP.md docs/superpowers/plans
git commit -m "docs: checkpoint git-to-host deployment workflow"
```

---

## Follow-up Plan Boundary: Android Application Host + Keystore

The approved spec also requires a real Android application/platform host and Android Keystore-backed implementation of the existing token-protection boundary. That work is intentionally **not mixed into this plan** because it introduces an Android target/workload, application lifecycle, manifest/package configuration and device-specific secure-storage tests independent of the Git-to-Host domain workflow above.

After Task 9 is green, create a separate spec-derived implementation plan for:

- Android application project/host,
- DI/composition of `RaayaGitDeploy.Android.Core`,
- `IPlatformAccessTokenProtector` backed by Android Keystore,
- protected persistence and session lifecycle,
- repository/profile/pending-plan UI,
- device/emulator acceptance tests.

The platform host must consume the plan-based companion contract from Task 9 and must not reintroduce raw deployment credentials or arbitrary path deployment.
