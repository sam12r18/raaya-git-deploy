# Git-to-Host Phase 1 Core Automation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first working automation spine for Raaya Git Deploy: track the last successful deployed commit per repository/server profile, safely update Git, derive the full pending commit/file range, auto-build a deployment queue (including generated-output rules), pin Dry Run to a target SHA, and advance the baseline only after full success.

**Architecture:** Reuse the current `IGitRepositoryService`, `DeploymentPlanner`, `DeploymentExecutor`, `IDeploymentHistoryStore`, server profiles and transport abstraction. This phase changes shared Core + Infrastructure only far enough to produce a deterministic pending deployment plan; Desktop UI and Mobile companion consume these contracts in follow-up plans. Android application-host/Keystore remains a separate platform plan.

**Tech Stack:** .NET 10 / C# 14, git.exe through the existing `IGitProcessRunner`, System.Text.Json, xUnit, existing Core/Infrastructure test projects.

**Spec:** `docs/superpowers/specs/2026-09-24-git-to-host-deployment-workflow-design.md`

## Global Constraints

- Canonical pending range is `last_successfully_deployed_head(repository, profile)..current_local_head`, not the most recent Pull range.
- Multiple Pulls before deployment must accumulate into one pending range.
- Failed, partial, blocked or cancelled deployment must never advance the successful baseline.
- Automated Pull requires a clean working tree, configured upstream and fast-forward-only behavior.
- Pending deployment contains committed Git state only; uncommitted working-tree files are not silently included.
- Queue provenance distinguishes Git-detected, generated-rule and manual items.
- Added/Modified map to Upload; Deleted maps to Delete; Renamed maps to Upload(new) + Delete(old).
- Delete operations remain subject to existing upload-before-delete safety.
- Generated rules only include files that already exist locally; this phase does not auto-run build commands.
- Dry Run is pinned to an exact repository/profile/from/to SHA context.
- Existing history JSON remains readable.
- FTP/FTPS/SFTP credentials remain Infrastructure-only and are not added to shared plan/history models.
- Do not change or merge `feat/git-host-auto-deploy` in this plan.

## Review Focus

1. **Legacy history JSON** with no Git metadata loads successfully but cannot become a deployed Git baseline.
2. **Equivalent Windows repository paths** with casing differences resolve the same baseline.
3. **Several Pulls before Deploy** still produce the full original deployed HEAD → current HEAD range.
4. **Rename plus failed upload** keeps the old-path delete blocked and does not advance the baseline.
5. **HEAD changes after Dry Run** rejects execution of the stale reviewed plan.

---

### Task 1: Persist Git-aware deployment history and resolve the last successful baseline

**Files:**
- Modify: `src/RaayaGitDeploy.Core/Deployment/IDeploymentHistoryStore.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/DeploymentBaselineService.cs`
- Modify: `src/RaayaGitDeploy.Infrastructure/Deployment/JsonDeploymentHistoryStore.cs`
- Create: `tests/RaayaGitDeploy.Core.Tests/Deployment/DeploymentBaselineServiceTests.cs`
- Create or extend: `tests/RaayaGitDeploy.Infrastructure.Tests/Deployment/JsonDeploymentHistoryStoreTests.cs`

**Interfaces:**
- Produces `DeploymentHistoryEntry.RepositoryPath`, `Branch`, `FromHead`, `ToHead`, `FinishedAt` as optional trailing metadata.
- Produces `DeploymentBaselineService.GetLastSuccessfulAsync(string repositoryPath, string serverProfileId, CancellationToken)`.

- [ ] **Step 1: Write failing Core tests**

Add this helper and tests to `DeploymentBaselineServiceTests.cs`:

```csharp
private sealed class MemoryHistoryStore(IReadOnlyList<DeploymentHistoryEntry> entries) : IDeploymentHistoryStore
{
    public Task<IReadOnlyList<DeploymentHistoryEntry>> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(entries);

    public Task AppendAsync(DeploymentHistoryEntry entry, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

private static DeploymentHistoryEntry Entry(
    string id, bool succeeded, string repositoryPath, string profileId, string toHead, DateTimeOffset startedAt) =>
    new(id, startedAt, profileId, "Production", succeeded, [], repositoryPath, "main", "base", toHead, startedAt.AddMinutes(1));

[Fact]
public async Task Returns_latest_successful_git_entry_for_equivalent_windows_path()
{
    var store = new MemoryHistoryStore([
        Entry("1", true,  @"C:\work\app", "prod", "aaa", DateTimeOffset.Parse("2026-09-24T10:00:00Z")),
        Entry("2", false, @"C:\work\app", "prod", "bbb", DateTimeOffset.Parse("2026-09-24T11:00:00Z")),
        Entry("3", true,  @"c:\WORK\app", "prod", "ccc", DateTimeOffset.Parse("2026-09-24T12:00:00Z"))
    ]);

    var baseline = await new DeploymentBaselineService(store)
        .GetLastSuccessfulAsync(@"C:\work\app", "prod", CancellationToken.None);

    Assert.NotNull(baseline);
    Assert.Equal("ccc", baseline!.ToHead);
}

[Fact]
public async Task Legacy_and_failed_entries_do_not_form_git_baseline()
{
    var store = new MemoryHistoryStore([
        new DeploymentHistoryEntry("legacy", DateTimeOffset.UtcNow, "prod", "Production", true, []),
        Entry("failed", false, @"C:\work\app", "prod", "bbb", DateTimeOffset.UtcNow)
    ]);

    var baseline = await new DeploymentBaselineService(store)
        .GetLastSuccessfulAsync(@"C:\work\app", "prod", CancellationToken.None);

    Assert.Null(baseline);
}
```

- [ ] **Step 2: Run the focused Core tests and confirm they fail**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --filter DeploymentBaselineServiceTests
```

Expected: FAIL because Git metadata and `DeploymentBaselineService` do not exist.

- [ ] **Step 3: Expand `DeploymentHistoryEntry` compatibly**

Use optional trailing fields so existing constructors and old JSON remain valid:

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

- [ ] **Step 4: Implement the baseline resolver**

```csharp
public sealed class DeploymentBaselineService(IDeploymentHistoryStore store)
{
    public async Task<DeploymentHistoryEntry?> GetLastSuccessfulAsync(
        string repositoryPath,
        string serverProfileId,
        CancellationToken cancellationToken)
    {
        var target = Normalize(repositoryPath);
        var entries = await store.LoadAsync(cancellationToken);

        return entries
            .Where(entry => entry.Succeeded &&
                            !string.IsNullOrWhiteSpace(entry.RepositoryPath) &&
                            !string.IsNullOrWhiteSpace(entry.ToHead) &&
                            string.Equals(Normalize(entry.RepositoryPath!), target, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(entry.ServerProfileId, serverProfileId, StringComparison.Ordinal))
            .OrderByDescending(entry => entry.FinishedAt ?? entry.StartedAt)
            .FirstOrDefault();
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
```

- [ ] **Step 5: Add legacy JSON and round-trip Infrastructure tests**

The legacy test writes this exact shape and asserts nullable Git fields are null after load:

```json
[{"Id":"legacy-1","StartedAt":"2026-09-20T10:00:00+00:00","ServerProfileId":"prod","ServerDisplayName":"Production","Succeeded":true,"Items":[]}]
```

The round-trip test appends an entry with repository `C:\work\app`, branch `main`, `FromHead="aaa"`, `ToHead="bbb"`, reloads it, and asserts those five Git/timing fields survive unchanged.

- [ ] **Step 6: Run Core + Infrastructure suites**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/RaayaGitDeploy.Core/Deployment/IDeploymentHistoryStore.cs src/RaayaGitDeploy.Core/Deployment/DeploymentBaselineService.cs src/RaayaGitDeploy.Infrastructure/Deployment/JsonDeploymentHistoryStore.cs tests/RaayaGitDeploy.Core.Tests/Deployment tests/RaayaGitDeploy.Infrastructure.Tests/Deployment
git commit -m "feat(core): track last successful deployed head"
```

---

### Task 2: Add safe fast-forward-only Git Update/Pull

**Files:**
- Create: `src/RaayaGitDeploy.Core/Git/IGitUpdateService.cs`
- Create: `src/RaayaGitDeploy.Infrastructure/GitCli/GitUpdateService.cs`
- Create: `tests/RaayaGitDeploy.Infrastructure.Tests/GitCli/GitUpdateServiceTests.cs`

**Interfaces:**

```csharp
public sealed record GitUpdateResult(
    string RepositoryPath,
    string Branch,
    string OldHead,
    string NewHead,
    bool Changed);

public interface IGitUpdateService
{
    Task<GitUpdateResult> UpdateFastForwardOnlyAsync(
        string repositoryPath,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write failing command-sequence tests**

Use a recording fake implementing the existing `IGitProcessRunner` contract and feed results in call order. Pin these command sequences exactly:

```csharp
var successfulCommands = new[]
{
    "status --porcelain=v2 -z --untracked-files=all",
    "branch --show-current",
    "rev-parse --abbrev-ref --symbolic-full-name @{u}",
    "rev-parse HEAD",
    "pull --ff-only",
    "rev-parse HEAD"
};
```

Tests:

```csharp
[Fact]
public async Task Dirty_working_tree_stops_before_upstream_or_pull()
{
    var runner = RecordingGitProcessRunner.WithResults(
        new GitCommandResult(0, "? untracked.txt\0", ""));

    var service = new GitUpdateService(runner);
    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        service.UpdateFastForwardOnlyAsync(@"C:\repo", CancellationToken.None));

    Assert.Single(runner.Calls);
    Assert.Equal("status --porcelain=v2 -z --untracked-files=all", runner.Calls[0]);
}

[Fact]
public async Task Successful_update_uses_ff_only_and_returns_before_after_heads()
{
    var runner = RecordingGitProcessRunner.WithResults(
        new GitCommandResult(0, "", ""),
        new GitCommandResult(0, "main\n", ""),
        new GitCommandResult(0, "origin/main\n", ""),
        new GitCommandResult(0, "aaaaaaaa\n", ""),
        new GitCommandResult(0, "Updating aaaaaaaa..bbbbbbbb\n", ""),
        new GitCommandResult(0, "bbbbbbbb\n", ""));

    var result = await new GitUpdateService(runner)
        .UpdateFastForwardOnlyAsync(@"C:\repo", CancellationToken.None);

    Assert.Equal("aaaaaaaa", result.OldHead);
    Assert.Equal("bbbbbbbb", result.NewHead);
    Assert.True(result.Changed);
    Assert.Contains("pull --ff-only", runner.Calls);
}
```

At the bottom of the test file, define `RecordingGitProcessRunner` with a `Queue<GitCommandResult>`, a public `List<string> Calls`, and `RunAsync` that records `string.Join(' ', arguments)` before dequeuing the next result.

- [ ] **Step 2: Run focused tests and confirm failure**

```powershell
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug --filter GitUpdateServiceTests
```

- [ ] **Step 3: Implement update policy**

Implementation order:

```text
git status --porcelain=v2 -z --untracked-files=all
→ require empty output
git branch --show-current
→ require non-empty branch
git rev-parse --abbrev-ref --symbolic-full-name @{u}
→ require exit code 0 and non-empty upstream
git rev-parse HEAD
→ OldHead
git pull --ff-only
→ require exit code 0
git rev-parse HEAD
→ NewHead
```

For every non-zero required command, throw `InvalidOperationException` containing the failed command and stderr/stdout message. Do not run merge/rebase fallback.

- [ ] **Step 4: Run Infrastructure suite**

```powershell
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/RaayaGitDeploy.Core/Git/IGitUpdateService.cs src/RaayaGitDeploy.Infrastructure/GitCli/GitUpdateService.cs tests/RaayaGitDeploy.Infrastructure.Tests/GitCli/GitUpdateServiceTests.cs
git commit -m "feat(git): add safe fast-forward update"
```

---

### Task 3: Add exact committed range queries

**Files:**
- Modify: `src/RaayaGitDeploy.Core/Git/IGitRepositoryService.cs`
- Modify: `src/RaayaGitDeploy.Infrastructure/GitCli/GitRepositoryService.cs`
- Extend: `tests/RaayaGitDeploy.Infrastructure.Tests/GitCli/GitRepositoryServiceTests.cs`

**Interfaces:**

```csharp
Task<IReadOnlyList<GitCommitInfo>> GetCommitsBetweenAsync(
    string repositoryPath,
    string baseRef,
    string targetRef,
    CancellationToken cancellationToken);

Task<IReadOnlyList<GitChange>> GetChangesBetweenAsync(
    string repositoryPath,
    string baseRef,
    string targetRef,
    CancellationToken cancellationToken);
```

- [ ] **Step 1: Add exact-range tests**

The commit query test must assert use of the same machine-readable format already used by `GetRecentCommitsAsync`:

```csharp
var format = "%H\u001f%s\u001f%an\u001f%aI\u001e";
var expected = $"log --format={format} aaaaaaaa..cccccccc";
```

The change query test must assert:

```text
diff --name-status -M -z aaaaaaaa cccccccc
```

Feed a rename payload such as `R100\0old.php\0new.php\0` and assert `GitChange("new.php", Renamed, "old.php")`.

- [ ] **Step 2: Run focused Infrastructure tests and confirm failure**

```powershell
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug --filter GitRepositoryServiceTests
```

- [ ] **Step 3: Implement both range methods**

Resolve both refs through the existing `ResolveCommitAsync`. For commits use:

```csharp
var format = $"%H{CommitFieldSeparator}%s{CommitFieldSeparator}%an{CommitFieldSeparator}%aI{CommitRecordSeparator}";
var arguments = new[] { "log", $"--format={format}", $"{baseCommit}..{targetCommit}" };
```

For changed paths use:

```csharp
var arguments = new[] { "diff", "--name-status", "-M", "-z", baseCommit, targetCommit };
```

Parse with the existing `ParseCommitHistory` and `GitNameStatusParser` paths. These methods must not inspect the working tree.

- [ ] **Step 4: Run Infrastructure suite**

```powershell
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/RaayaGitDeploy.Core/Git/IGitRepositoryService.cs src/RaayaGitDeploy.Infrastructure/GitCli/GitRepositoryService.cs tests/RaayaGitDeploy.Infrastructure.Tests/GitCli
git commit -m "feat(git): query exact deployment commit ranges"
```

---

### Task 4: Build pending deployment snapshot from deployed HEAD to current HEAD

**Files:**
- Create: `src/RaayaGitDeploy.Core/Deployment/PendingDeploymentSnapshot.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/PendingDeploymentService.cs`
- Create: `tests/RaayaGitDeploy.Core.Tests/Deployment/PendingDeploymentServiceTests.cs`

**Interfaces:**

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

`PendingDeploymentService` consumes `IGitRepositoryService` + `DeploymentBaselineService`.

- [ ] **Step 1: Write failing service tests with explicit fakes**

Create `FakeGitRepositoryService : IGitRepositoryService` in the test file with configurable `Context`, `CommitsBetween`, `ChangesBetween`, and captured `RequestedBase`/`RequestedTarget`; non-used interface members throw `NotSupportedException`.

Add:

```csharp
[Fact]
public async Task Multiple_updates_still_use_last_deployed_head_as_range_start()
{
    var git = FakeGitRepositoryService.AtHead("cccccccc");
    git.CommitsBetween = [new("bbbbbbbb", "bbbbbbbb", "B", "Dev", DateTimeOffset.UtcNow),
                          new("cccccccc", "cccccccc", "C", "Dev", DateTimeOffset.UtcNow)];
    git.ChangesBetween = [new("app.php", GitChangeKind.Modified)];

    var history = new MemoryHistoryStore([
        new DeploymentHistoryEntry("deploy-a", DateTimeOffset.UtcNow, "prod", "Production", true, [], @"C:\repo", "main", null, "aaaaaaaa")
    ]);

    var service = new PendingDeploymentService(git, new DeploymentBaselineService(history));
    var snapshot = await service.BuildAsync(@"C:\repo", "prod", CancellationToken.None);

    Assert.Equal("aaaaaaaa", snapshot.FromHead);
    Assert.Equal("cccccccc", snapshot.ToHead);
    Assert.Equal("aaaaaaaa", git.RequestedBase);
    Assert.Equal("cccccccc", git.RequestedTarget);
}

[Fact]
public async Task Missing_git_aware_baseline_returns_baseline_required_without_guessing()
{
    var git = FakeGitRepositoryService.AtHead("cccccccc");
    var service = new PendingDeploymentService(git, new DeploymentBaselineService(new MemoryHistoryStore([])));

    var snapshot = await service.BuildAsync(@"C:\repo", "prod", CancellationToken.None);

    Assert.True(snapshot.RequiresBaseline);
    Assert.Null(snapshot.FromHead);
    Assert.Empty(snapshot.Commits);
    Assert.Empty(snapshot.Changes);
}
```

Reuse the `MemoryHistoryStore` shape from Task 1 in this test file.

- [ ] **Step 2: Run focused Core tests and confirm failure**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --filter PendingDeploymentServiceTests
```

- [ ] **Step 3: Implement snapshot generation**

Algorithm:

```text
context = GetContextAsync(repository)
baseline = GetLastSuccessfulAsync(context.RootPath, profileId)
if baseline == null → RequiresBaseline=true, FromHead=null, no range query
if baseline.ToHead == context.HeadSha → empty commits/changes, RequiresBaseline=false
otherwise → GetCommitsBetweenAsync(baseline.ToHead, context.HeadSha)
          → GetChangesBetweenAsync(baseline.ToHead, context.HeadSha)
```

Do not call `GetWorkingTreeChangesAsync` from this service.

- [ ] **Step 4: Run Core suite**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/RaayaGitDeploy.Core/Deployment/PendingDeploymentSnapshot.cs src/RaayaGitDeploy.Core/Deployment/PendingDeploymentService.cs tests/RaayaGitDeploy.Core.Tests/Deployment/PendingDeploymentServiceTests.cs
git commit -m "feat(deploy): derive pending deployment range"
```

---

### Task 5: Auto-build queue from Git changes and generated-output rules

**Files:**
- Modify: `src/RaayaGitDeploy.Core/Deployment/DeploymentQueueItem.cs`
- Modify: `src/RaayaGitDeploy.Core/Deployment/DeploymentPlanner.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/DeploymentRuleSet.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/PendingDeploymentQueueBuilder.cs`
- Create: `tests/RaayaGitDeploy.Core.Tests/Deployment/PendingDeploymentQueueBuilderTests.cs`
- Extend: `tests/RaayaGitDeploy.Core.Tests/Deployment/DeploymentPlannerTests.cs`

**Interfaces:**

```csharp
public enum DeploymentQueueSource
{
    GitSelection,
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

public sealed record DeploymentRuleSet(IReadOnlyList<string> GeneratedPaths);
```

`GeneratedPaths` in Phase 1 are repository-relative literal files/directories, for example `public/build`. Directory rules recursively include existing files beneath that directory; missing generated paths produce a warning returned by the builder rather than silently disappearing.

- [ ] **Step 1: Write Git mapping tests**

Given changes:

```csharp
var changes = new GitChange[]
{
    new("new.php", GitChangeKind.Added),
    new("app.php", GitChangeKind.Modified),
    new("gone.php", GitChangeKind.Deleted),
    new("renamed.php", GitChangeKind.Renamed, "old.php")
};
```

Assert queue contains exactly:

```text
Upload new.php      / GitDetected
Upload app.php      / GitDetected
Delete gone.php     / GitDetected
Upload renamed.php  / GitDetected
Delete old.php      / GitDetected
```

- [ ] **Step 2: Write generated-rule and deduplication tests**

Create a temp repository with `public/build/app.js` and `public/build/app.css`; rule `public/build` must add both as `GeneratedRule` uploads. If `app.js` is already a GitDetected upload, assert only one upload for the normalized path and its source is `GeneratedRule`. Add a missing-rule test asserting warning text contains the missing repository-relative path.

- [ ] **Step 3: Write planner delete tests**

A delete item for `gone.php` must map to `/remote/gone.php` without requiring the local file to exist. A delete item whose local path resolves outside repository root must throw `InvalidOperationException`.

- [ ] **Step 4: Run focused Core tests and confirm failure**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --filter "PendingDeploymentQueueBuilderTests|DeploymentPlannerTests"
```

- [ ] **Step 5: Implement queue builder and planner overload**

Add:

```csharp
public DeploymentPlan Plan(
    string repositoryRoot,
    string remoteRoot,
    IEnumerable<DeploymentQueueItem> items,
    bool dryRun = false)
```

For `Upload`, emit `DeploymentOperationKind.Upload`; for `Delete`, emit `DeploymentOperationKind.Delete`. Preserve the existing string-path overload by wrapping paths as upload items.

Normalize/dedupe uploads with `Path.GetFullPath`. Generated-rule source outranks GitDetected for identical upload paths because it explains why a build artifact is intentionally included; explicit manual items remain intact as user intent.

- [ ] **Step 6: Run Core suite**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/RaayaGitDeploy.Core/Deployment tests/RaayaGitDeploy.Core.Tests/Deployment
git commit -m "feat(deploy): auto-build queue from pending git changes"
```

---

### Task 6: Pin Dry Run to SHA and advance baseline only after full success

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

`DeploymentRunCoordinator` consumes `IGitRepositoryService`, `DeploymentExecutor`, `IDeploymentHistoryStore`.

- [ ] **Step 1: Write stale-preview test**

Build a plan pinned to `ToHead="cccccccc"`; configure fake Git context at `dddddddd`; call coordinator execution and assert `InvalidOperationException` before `DeploymentExecutor.ExecuteAsync` is invoked.

- [ ] **Step 2: Write failed-result baseline test**

Use a fake transport whose upload throws for the first upload and include one delete operation. Assert result contains failed upload + blocked delete, appended history has `Succeeded=false`, `FromHead="aaaaaaaa"`, `ToHead="cccccccc"`, and `DeploymentBaselineService` still resolves the older successful `aaaaaaaa` entry.

- [ ] **Step 3: Write successful-result baseline test**

Use a successful fake transport. Assert appended history has `Succeeded=true`, repository/branch/profile context, exact `aaaaaaaa → cccccccc`, non-null `FinishedAt`; then assert `DeploymentBaselineService` resolves `cccccccc`.

- [ ] **Step 4: Run focused Core tests and confirm failure**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --filter DeploymentRunCoordinatorTests
```

- [ ] **Step 5: Implement coordinator**

Execution rules:

```text
reject plan.IsDryRun == true for mutation
require non-null plan.Context
load current Git context
require current HeadSha == plan.Context.ToHead
execute through existing DeploymentExecutor
append history for success or failure with exact reviewed Git context
return DeploymentResult
```

Do not update a separate mutable “last deployed SHA” file in Phase 1; successful history is the auditable source used by `DeploymentBaselineService`.

- [ ] **Step 6: Run full Phase 1 matrix**

```powershell
dotnet restore RaayaGitDeploy.slnx
dotnet build RaayaGitDeploy.slnx -c Debug --no-restore
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --no-build
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug --no-build
dotnet test tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj -c Debug --no-build
dotnet test tests/RaayaGitDeploy.Android.Core.Tests/RaayaGitDeploy.Android.Core.Tests.csproj -c Debug --no-build
```

Only mark commands `TESTED-PASS` if they actually exit successfully.

- [ ] **Step 7: Commit**

```bash
git add src/RaayaGitDeploy.Core/Deployment tests/RaayaGitDeploy.Core.Tests/Deployment
git commit -m "feat(deploy): pin reviewed plan to deployed git baseline"
```

---

## Phase 1 Acceptance

The phase is complete only when these scenarios are covered by tests and the complete solution remains green:

```text
Production baseline A
Pull/update to B
Pull/update to C without deploy
→ pending range is A..C
→ commits/files come from exact committed range
→ queue is generated automatically
→ generated rule output such as public/build joins queue when present
→ Dry Run is pinned to target C
→ failed upload blocks deletes and leaves baseline A
→ successful full deployment records A→C
→ next pending range at HEAD C is empty
→ HEAD D after reviewing C makes the C preview stale
```

## Subsequent Plans from the Same Approved Spec

After Phase 1 is green, write and execute these as separate plans so each remains reviewable:

1. **Desktop Workflow Plan** — wire `IGitUpdateService`, `PendingDeploymentService`, generated rules, queue, Dry Run and coordinator into `DeploymentWorkspaceViewModel` and `RepositoryWorkspacePage`; add explicit initial-baseline onboarding; make **Update → Review Pending → Dry Run → Deploy** the dominant UX; finish concrete FTP/FTPS/SFTP composition and secure credential resolver; preserve responsive navigation/split panes and existing IDE-style Commit/Terminal work.
2. **Companion/Mobile Plan** — replace arbitrary path submission with agent-issued pending plan IDs + item IDs; expose pending commits/files, Dry Run, confirmation, progress/result/history; keep all host credentials server-side.
3. **Android Host Plan** — create the actual Android application/platform host, compose `RaayaGitDeploy.Android.Core`, implement existing token-protection boundary with Android Keystore, and run emulator/device acceptance.
4. **Auto Deploy Integration Plan** — compare `feat/git-host-auto-deploy` against the now-stable pending-range/plan/history contracts and reconcile explicitly without blind merge or policy leakage.
