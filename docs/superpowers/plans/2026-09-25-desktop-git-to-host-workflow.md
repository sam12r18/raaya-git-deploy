# Desktop Git-to-Host Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the green Git-to-Host Core contracts into the primary Windows Desktop workflow: Update → Review Pending → Auto Queue → Dry Run → Deploy → History, with explicit first-baseline onboarding and working SFTP/FTP/FTPS composition.

**Architecture:** Keep Git/deployment semantics in Core and Infrastructure, and make Presentation orchestrate already-reviewed contracts rather than rebuilding ad-hoc file lists. `DeploymentWorkspaceViewModel` becomes the Desktop coordinator while a focused `PendingDeploymentViewModel` owns update/pending/rule state; `RepositoryWorkspacePage` renders the workflow without absorbing business logic. Transport secrets stay in the Desktop/Infrastructure boundary; history and deployment plans remain transport-neutral.

**Tech Stack:** .NET 10 / C# 14, WinUI 3, CommunityToolkit.Mvvm, git.exe via existing `IGitProcessRunner`, FluentFTP, SSH.NET, Windows Credential Manager, System.Text.Json, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-24-git-to-host-deployment-workflow-design.md`

## Global Constraints

- Canonical pending range remains `last_successfully_deployed_head(repository, profile)..current_local_head`.
- `Update` uses the existing clean-tree + upstream + `git pull --ff-only` policy; Desktop must surface failures without altering the deployment baseline.
- No baseline is guessed. First use requires an explicit audited choice: mark current HEAD, mark a selected ancestor commit, or use a selected ancestor as the base for the first managed deployment.
- Dry Run and Deploy use the same `DeploymentPlan.Operations` and `DeploymentPlanContext`; execution never reconstructs a path-only plan.
- Queue items preserve `Action` and `Source`; Git deletes/rename-old remain explicit Delete operations.
- Refreshing automatic queue items must preserve explicit manual user items.
- FTP/FTPS passwords never enter `ServerProfile`, deployment plan, history JSON, or Mobile contracts.
- FTPS remains fail-closed on certificate validation. Do not add an “accept any certificate” shortcut.
- Do not change, merge, or cherry-pick `feat/git-host-auto-deploy`; reconcile that branch only in its later dedicated integration plan.
- Preserve existing Commits, Changes, Terminal, Commands, Servers and History capabilities. Do not trade core workflow completion for unrelated visual polish.
- Keep new code split into focused files/partials instead of growing `RepositoryWorkspacePage.xaml.cs` and `DeploymentWorkspaceViewModel.cs` into monoliths.

## Review Focus

1. **Dirty working tree during Update** — operation fails with actionable text, current repository state stays visible, pending baseline is unchanged.
2. **Server profile changes after Dry Run** — old preview cannot execute against the new profile; a new pending refresh/Dry Run is required.
3. **HEAD changes after Dry Run** — `DeploymentRunCoordinator` rejects the stale preview before remote mutation.
4. **Manual queue item overlaps an automatic/generated item** — keep one effective operation while preserving explicit user intent; do not silently duplicate uploads.
5. **Missing/invalid Desktop credential or invalid FTPS certificate** — connection/deploy fails safely and no secret is persisted in profile/history/log text.

---

### Task 1: Add explicit, auditable initial baseline onboarding

**Files:**
- Modify: `src/RaayaGitDeploy.Core/Deployment/IDeploymentHistoryStore.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/DeploymentBaselineOnboardingService.cs`
- Modify: `src/RaayaGitDeploy.Core/Git/IGitRepositoryService.cs`
- Modify: `src/RaayaGitDeploy.Infrastructure/GitCli/GitRepositoryService.cs`
- Extend: `tests/RaayaGitDeploy.Core.Tests/Deployment/DeploymentBaselineServiceTests.cs`
- Create: `tests/RaayaGitDeploy.Core.Tests/Deployment/DeploymentBaselineOnboardingServiceTests.cs`
- Create: `tests/RaayaGitDeploy.Infrastructure.Tests/GitCli/GitRepositoryAncestorTests.cs`

**Interfaces:**

```csharp
public enum DeploymentHistoryEventKind
{
    Deployment = 0,
    BaselineMarked = 1
}

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
    DateTimeOffset? FinishedAt = null,
    DeploymentHistoryEventKind EventKind = DeploymentHistoryEventKind.Deployment);

Task<bool> IsAncestorAsync(
    string repositoryPath,
    string ancestorRef,
    string descendantRef,
    CancellationToken cancellationToken);
```

`DeploymentBaselineOnboardingService` produces:

```csharp
Task<DeploymentHistoryEntry> MarkCurrentHeadAsync(
    string repositoryPath,
    ServerProfile profile,
    CancellationToken cancellationToken);

Task<DeploymentHistoryEntry> MarkCommitAsync(
    string repositoryPath,
    string commitRef,
    ServerProfile profile,
    CancellationToken cancellationToken);
```

- [ ] **Step 1: Write RED Core tests for current-HEAD and selected-commit baseline marking**

Assert `MarkCurrentHeadAsync` appends a successful zero-item history entry with `EventKind=BaselineMarked`, exact repository/branch/current `ToHead`, selected profile ID/name and non-null `FinishedAt`.

For `MarkCommitAsync`, configure current HEAD `cccccccc` and `IsAncestorAsync("aaaaaaaa", "cccccccc") == true`; assert the entry records `ToHead="aaaaaaaa"`. Configure a non-ancestor commit and assert `InvalidOperationException` before history append.

- [ ] **Step 2: Write RED Infrastructure test for ancestry**

Create a real temp Git repo with commits A → B. Assert `IsAncestorAsync(A, B)` is true and `IsAncestorAsync(B, A)` is false. The implementation command is exactly:

```text
git merge-base --is-ancestor <resolved-ancestor> <resolved-descendant>
```

Exit 0 = true, exit 1 = false, any other exit code = `InvalidOperationException` with Git stderr/stdout.

- [ ] **Step 3: Implement baseline event and onboarding service**

Use `IGitRepositoryService.GetContextAsync` to canonicalize repository/branch/HEAD. `MarkCommitAsync` accepts current HEAD directly; otherwise it requires `IsAncestorAsync(commitRef, current.HeadSha)`. Append the audited entry through `IDeploymentHistoryStore`; do not create or mutate a separate “last deployed SHA” file.

- [ ] **Step 4: Run focused tests**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --filter "DeploymentBaselineOnboardingServiceTests|DeploymentBaselineServiceTests"
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug --filter GitRepositoryAncestorTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/RaayaGitDeploy.Core/Deployment src/RaayaGitDeploy.Core/Git/IGitRepositoryService.cs src/RaayaGitDeploy.Infrastructure/GitCli/GitRepositoryService.cs tests/RaayaGitDeploy.Core.Tests/Deployment tests/RaayaGitDeploy.Infrastructure.Tests/GitCli/GitRepositoryAncestorTests.cs
git commit -m "feat(deploy): add explicit baseline onboarding"
```

---

### Task 2: Support an explicit base for the first managed deployment

**Files:**
- Modify: `src/RaayaGitDeploy.Core/Deployment/PendingDeploymentService.cs`
- Extend: `tests/RaayaGitDeploy.Core.Tests/Deployment/PendingDeploymentServiceTests.cs`

**Produces:**

```csharp
Task<PendingDeploymentSnapshot> BuildFromExplicitBaseAsync(
    string repositoryPath,
    string baseRef,
    CancellationToken cancellationToken);
```

- [ ] **Step 1: Write RED tests**

For repo HEAD `cccccccc` and selected base `aaaaaaaa`, configure ancestry true and return two commits + one changed file. Assert snapshot is `aaaaaaaa → cccccccc`, `RequiresBaseline=false`, and exact range methods receive those SHAs.

Add a non-ancestor test and assert the service rejects it instead of creating a misleading first-deploy range.

- [ ] **Step 2: Implement explicit-base snapshot**

Resolve current context, require `baseRef == current.HeadSha || IsAncestorAsync(baseRef, current.HeadSha)`, return an empty snapshot when equal, otherwise use the existing exact committed range methods. Never inspect working-tree changes here.

- [ ] **Step 3: Run Core tests**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --filter PendingDeploymentServiceTests
```

Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/RaayaGitDeploy.Core/Deployment/PendingDeploymentService.cs tests/RaayaGitDeploy.Core.Tests/Deployment/PendingDeploymentServiceTests.cs
git commit -m "feat(deploy): allow explicit first-deploy base"
```

---

### Task 3: Persist per-repository generated-output rules

**Files:**
- Create: `src/RaayaGitDeploy.Core/Deployment/IDeploymentRuleStore.cs`
- Create: `src/RaayaGitDeploy.Infrastructure/Deployment/JsonDeploymentRuleStore.cs`
- Create: `tests/RaayaGitDeploy.Infrastructure.Tests/Deployment/JsonDeploymentRuleStoreTests.cs`

**Interface:**

```csharp
public interface IDeploymentRuleStore
{
    Task<DeploymentRuleSet> LoadAsync(string repositoryPath, CancellationToken cancellationToken);
    Task SaveAsync(string repositoryPath, DeploymentRuleSet rules, CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write RED persistence tests**

Use a temp JSON path. Save `GeneratedPaths = ["public/build", "dist/app.js"]`, reload with the same repository path under different Windows casing, and assert the same rules. Assert a previously unseen repo returns an empty rule set. Assert JSON contains no server/profile/credential material.

- [ ] **Step 2: Implement JSON store**

Persist one document keyed by normalized full repository path using `StringComparer.OrdinalIgnoreCase` semantics on read/update. Normalize rule paths to forward-slash repository-relative values, reject rooted paths and `..` traversal, deduplicate case-insensitively, and write atomically through temp-file + replace/move.

- [ ] **Step 3: Run Infrastructure tests**

```powershell
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug --filter JsonDeploymentRuleStoreTests
```

Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/RaayaGitDeploy.Core/Deployment/IDeploymentRuleStore.cs src/RaayaGitDeploy.Infrastructure/Deployment/JsonDeploymentRuleStore.cs tests/RaayaGitDeploy.Infrastructure.Tests/Deployment/JsonDeploymentRuleStoreTests.cs
git commit -m "feat(deploy): persist generated output rules"
```

---

### Task 4: Add pending-deployment Presentation state and automatic queue synchronization

**Files:**
- Create: `src/RaayaGitDeploy.Presentation/Deployment/PendingDeploymentViewModel.cs`
- Modify: `src/RaayaGitDeploy.Presentation/Deployment/DeploymentQueueViewModel.cs`
- Create: `tests/RaayaGitDeploy.Presentation.Tests/Deployment/PendingDeploymentViewModelTests.cs`
- Extend: `tests/RaayaGitDeploy.Presentation.Tests/Deployment/DeploymentQueueViewModelTests.cs`

**`PendingDeploymentViewModel` dependencies:**
- `IGitUpdateService`
- `PendingDeploymentService`
- `PendingDeploymentQueueBuilder`
- `IDeploymentRuleStore`
- `DeploymentQueueViewModel`

**State:**

```csharp
PendingDeploymentSnapshot? Snapshot
GitUpdateResult? LastUpdate
DeploymentRuleSet Rules
IReadOnlyList<string> Warnings
bool IsBusy
string? StatusMessage
int PendingCommitCount
int PendingChangeCount
bool RequiresBaseline
```

**Operations:**

```csharp
Task LoadAsync(string repositoryPath, string serverProfileId, CancellationToken cancellationToken);
Task<GitUpdateResult> UpdateAsync(string repositoryPath, string serverProfileId, CancellationToken cancellationToken);
Task UseExplicitBaseAsync(string repositoryPath, string baseRef, CancellationToken cancellationToken);
Task SaveRulesAsync(string repositoryPath, IReadOnlyList<string> generatedPaths, string serverProfileId, CancellationToken cancellationToken);
```

- [ ] **Step 1: Write RED queue merge tests**

Add `DeploymentQueueViewModel.ReplaceAutomaticItems(IEnumerable<DeploymentQueueItem>)` and `long Revision` expectations. Starting with ManualFile + GitSelection items, replace automatic items twice and assert manual/legacy explicit selections survive while old GitDetected/GeneratedRule entries are replaced. If an automatic upload overlaps a manual upload, retain one effective manual item. Revision increments on every semantic mutation, not on no-op duplicate adds.

- [ ] **Step 2: Write RED pending workflow tests**

`LoadAsync` must load rules, build profile-specific pending snapshot, run `PendingDeploymentQueueBuilder`, synchronize automatic queue items, publish warnings/counts and preserve manual items.

`UpdateAsync` must invoke `IGitUpdateService.UpdateFastForwardOnlyAsync`, then reload the full pending range from last deployed HEAD — not merely `OldHead..NewHead`. Configure a dirty-tree exception and assert existing Snapshot/Queue stay intact while `StatusMessage` reports the failure.

- [ ] **Step 3: Implement view model and queue synchronization**

Use a single-operation busy gate. Build all replacement state first, then publish Snapshot/Warnings/automatic queue so a failed refresh does not partially clear the previous useful state.

- [ ] **Step 4: Run Presentation tests**

```powershell
dotnet test tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj -c Debug --filter "PendingDeploymentViewModelTests|DeploymentQueueViewModelTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/RaayaGitDeploy.Presentation/Deployment tests/RaayaGitDeploy.Presentation.Tests/Deployment
git commit -m "feat(desktop): surface pending deployment state"
```

---

### Task 5: Make Dry Run and Deploy use one SHA-pinned reviewed plan

**Files:**
- Modify: `src/RaayaGitDeploy.Presentation/Deployment/DeploymentDryRunViewModel.cs`
- Modify: `src/RaayaGitDeploy.Presentation/Deployment/DeploymentWorkspaceViewModel.cs`
- Extend: `tests/RaayaGitDeploy.Presentation.Tests/Deployment/DeploymentDryRunViewModelTests.cs`
- Extend: `tests/RaayaGitDeploy.Presentation.Tests/Deployment/DeploymentWorkspaceViewModelTests.cs`
- Extend: `tests/RaayaGitDeploy.Presentation.Tests/Deployment/DeploymentExecutionConcurrencyTests.cs`
- Extend: `tests/RaayaGitDeploy.Presentation.Tests/Deployment/DeploymentRepositorySwitchTests.cs`

**Dry Run signature:**

```csharp
DeploymentPlan Preview(
    PendingDeploymentSnapshot snapshot,
    ServerProfile? selectedProfile,
    IEnumerable<DeploymentQueueItem> queueItems);
```

The returned dry-run plan has:

```csharp
Context = new DeploymentPlanContext(
    snapshot.RepositoryPath,
    snapshot.Branch,
    snapshot.FromHead,
    snapshot.ToHead,
    selectedProfile.Id)
```

- [ ] **Step 1: Write RED Dry Run tests**

Assert delete queue items remain Delete operations, queue Source is not discarded before planning, `Context` contains exact from/to/profile, and `RequiresBaseline=true` refuses preview. Empty effective queue also refuses preview.

- [ ] **Step 2: Write RED workspace execution tests**

Inject `PendingDeploymentViewModel`, `DeploymentBaselineOnboardingService`, `DeploymentRunCoordinator` and history store into `DeploymentWorkspaceViewModel`.

After Dry Run, store `Queue.Revision` and selected profile ID. Assert:
- queue mutation after preview rejects deploy before coordinator;
- selected profile change rejects deploy;
- unchanged inputs call coordinator with `PreviewPlan with { IsDryRun = false }`, preserving the exact reviewed Operations + Context;
- successful execution refreshes History and pending state;
- failed result refreshes History but leaves the original successful baseline/pending range;
- repository switch clears Snapshot/preview/result/automatic queue and refuses switching while active execution is running.

- [ ] **Step 3: Refactor workspace implementation**

Remove direct `_executor.ExecuteAsync` and the old simplistic history append from Presentation. Execution goes only through `DeploymentRunCoordinator`; history auditing and SHA validation stay in Core. Do not reconstruct the deploy plan with `Queue.Items.Select(item => item.LocalPath)`.

- [ ] **Step 4: Run Presentation suite**

```powershell
dotnet test tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj -c Debug
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/RaayaGitDeploy.Presentation/Deployment tests/RaayaGitDeploy.Presentation.Tests/Deployment
git commit -m "feat(desktop): pin dry run through deployment execution"
```

---

### Task 6: Compose SFTP + FTP + FTPS with Windows-protected FTP credentials

**Files:**
- Create: `src/RaayaGitDeploy.Infrastructure/Deployment/IDesktopCredentialStore.cs`
- Create: `src/RaayaGitDeploy.Infrastructure/Deployment/WindowsCredentialManagerStore.cs`
- Create: `src/RaayaGitDeploy.Infrastructure/Deployment/WindowsCredentialManagerFtpCredentialResolver.cs`
- Create: `tests/RaayaGitDeploy.Infrastructure.Tests/Deployment/WindowsCredentialManagerFtpCredentialResolverTests.cs`
- Modify: `src/RaayaGitDeploy.App/Bootstrap/ServiceRegistration.cs`

**Infrastructure boundary:**

```csharp
public interface IDesktopCredentialStore
{
    void Write(string targetName, string username, string secret);
    FtpCredential Read(string targetName);
    void Delete(string targetName);
}

public sealed class WindowsCredentialManagerFtpCredentialResolver(IDesktopCredentialStore store)
    : IFtpCredentialResolver
{
    public ValueTask<FtpCredential> ResolveAsync(ServerProfile profile, CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write RED resolver tests**

Use a fake `IDesktopCredentialStore`. Assert resolver reads exactly `profile.KeyReference`, returns username/password only in-memory, rejects blank/missing reference, and never writes the secret into `ServerProfile`.

- [ ] **Step 2: Implement Windows Credential Manager store**

Use `CredWriteW`, `CredReadW`, `CredFree`, `CredDeleteW` with `CRED_TYPE_GENERIC`. Copy credential blobs into managed memory only for the read duration and free native memory in `finally`. Exceptions may include target name/error code but never secret text.

- [ ] **Step 3: Replace single-SFTP registration with `RemoteTransportRouter`**

Register one SFTP transport and one `FtpRemoteTransport(() => new FluentFtpClientAdapter(), resolver)`, then map:

```csharp
new KeyValuePair<ServerTransportKind, IRemoteTransport>(ServerTransportKind.Sftp, sftp),
new KeyValuePair<ServerTransportKind, IRemoteTransport>(ServerTransportKind.Ftp, ftp),
new KeyValuePair<ServerTransportKind, IRemoteTransport>(ServerTransportKind.Ftps, ftp)
```

Register `IGitUpdateService`, `DeploymentBaselineService`, `DeploymentBaselineOnboardingService`, `PendingDeploymentService`, `PendingDeploymentQueueBuilder`, `IDeploymentRuleStore`, `DeploymentRunCoordinator`, and `PendingDeploymentViewModel` in the same composition pass.

- [ ] **Step 4: Run Infrastructure + solution build**

```powershell
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug
dotnet build RaayaGitDeploy.slnx -c Debug
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/RaayaGitDeploy.Infrastructure/Deployment src/RaayaGitDeploy.App/Bootstrap/ServiceRegistration.cs tests/RaayaGitDeploy.Infrastructure.Tests/Deployment
git commit -m "feat(desktop): compose ftp ftps and sftp transports"
```

---

### Task 7: Add secure FTP/FTPS credential editing to the Desktop server form

**Files:**
- Create: `src/RaayaGitDeploy.App/Services/DesktopServerCredentialService.cs`
- Modify: `src/RaayaGitDeploy.App/Views/RepositoryWorkspacePage.xaml`
- Modify: `src/RaayaGitDeploy.App/Views/RepositoryWorkspacePage.xaml.cs`
- Modify: `src/RaayaGitDeploy.App/Views/RepositoryWorkspacePage.Recovery.cs` only if recovery/profile binding needs the new selected-profile refresh; otherwise leave it untouched.

**Desktop-only service:**

```csharp
public sealed class DesktopServerCredentialService(IDesktopCredentialStore store)
{
    public string SaveFtpCredential(string profileId, string username, string password);
    public void DeleteFtpCredential(string credentialReference);
}
```

Credential target format is deterministic:

```text
RaayaGitDeploy/FTP/<profile-id>
```

- [ ] **Step 1: Add PasswordBox + transport-aware form behavior**

For FTP/FTPS, show `PasswordBox` labeled `Password (stored in Windows Credential Manager)` and make the existing reference field read-only/diagnostic or hide it. For SFTP, keep the SSH private-key path behavior. Never populate the PasswordBox from stored credentials.

- [ ] **Step 2: Coordinate save without persisting plaintext**

When saving FTP/FTPS:
1. establish/stabilize the profile ID;
2. require a password for a new profile, but allow blank password on edit to mean “keep existing credential”;
3. save/update the Windows credential and get its deterministic reference;
4. call `ServersViewModel.SaveDraftAsync` with that reference and `ExternalCredentialReference` policy generated by `ServerProfilePolicy`;
5. clear the PasswordBox immediately after save attempt.

When deleting an FTP/FTPS profile, delete its external credential after the profile delete succeeds. Error messages must not contain password text.

- [ ] **Step 3: Build Desktop App**

```powershell
dotnet build src/RaayaGitDeploy.App/RaayaGitDeploy.App.csproj -c Debug
```

Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/RaayaGitDeploy.App/Services/DesktopServerCredentialService.cs src/RaayaGitDeploy.App/Views/RepositoryWorkspacePage.xaml src/RaayaGitDeploy.App/Views/RepositoryWorkspacePage.xaml.cs
git commit -m "feat(desktop): protect ftp credentials in windows vault"
```

---

### Task 8: Make Update → Pending → Dry Run → Deploy the dominant Windows UI

**Files:**
- Modify: `src/RaayaGitDeploy.App/Views/RepositoryWorkspacePage.xaml`
- Create: `src/RaayaGitDeploy.App/Views/RepositoryWorkspacePage.DeploymentFlow.cs`
- Modify: `src/RaayaGitDeploy.App/Views/RepositoryWorkspacePage.xaml.cs`
- Modify: `src/RaayaGitDeploy.Presentation/Workspace/WorkspaceSection.cs` only if a dedicated `Deploy` section name replaces `DeployQueue`; otherwise keep the existing enum/tag for compatibility.

**UI structure:**

```text
Persistent deployment summary strip
  Repository / Branch / HEAD
  Profile selector
  Deployed HEAD
  Pending commits / files
  [Update] [Review Pending] [Dry Run] [Deploy]

Deploy workspace
  Baseline-required onboarding card OR pending-range content
  ├─ Pending commits (subject / author / date / SHA)
  ├─ Pending files (Action / Source / path)
  ├─ Generated paths + warnings
  └─ Dry Run exact remote operations + destructive warning
```

- [ ] **Step 1: Wire repository/profile lifecycle**

After successful repository open, load servers/history and load pending state for the selected profile. After profile selection changes, invalidate Preview and reload the profile-specific pending baseline. After `Update`, refresh `RepositoryWorkspaceViewModel` so branch/HEAD/working tree/commit browser agree with the deployment summary.

- [ ] **Step 2: Add explicit baseline onboarding card**

When `RequiresBaseline` is true, hide Deploy and present exactly three safe actions:
- `Mark current HEAD as deployed` → confirmation dialog → `MarkCurrentHeadAsync` → refresh pending;
- choose one commit from the existing recent commit list and `Mark selected commit as deployed` → confirmation → `MarkCommitAsync` → refresh;
- choose one commit and `Use as first-deploy base` → `UseExplicitBaseAsync`, without writing baseline history until deployment succeeds.

Confirmation text includes profile name + SHA. No action is preselected or automatic.

- [ ] **Step 3: Render pending review + generated rules**

Show pending commits/files in dense master/detail lists; make deletes visually explicit through the `Action` column/text rather than color alone. Add a small multiline generated-path editor (one repository-relative path per line) with Save; warnings from missing generated outputs remain visible before Dry Run.

- [ ] **Step 4: Render exact Dry Run and execution state**

Dry Run list shows operation kind, source/path, local path and remote path. Disable Deploy until a preview exists. Disable repository/profile/update/rule mutation while deployment is executing. A stale-preview error keeps the user on the Deploy workspace and prompts a new Dry Run.

- [ ] **Step 5: Apply the already-observed blocking UX fixes while touching this surface**

Move/rename `Deploy Queue` navigation to `Deploy` and place it before secondary tools. Prevent clipped nav labels at normal desktop width. Replace large fixed empty regions in Deploy with content-sized summary + a resizable pending/detail area (`GridSplitter` or equivalent WinUI split). Keep Commit/Terminal screens intact; do not redesign them in this task.

- [ ] **Step 6: Build and run Presentation tests**

```powershell
dotnet test tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj -c Debug
dotnet build src/RaayaGitDeploy.App/RaayaGitDeploy.App.csproj -c Debug
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/RaayaGitDeploy.App/Views src/RaayaGitDeploy.Presentation/Workspace
git commit -m "feat(desktop): center workspace on git-to-host deployment"
```

---

### Task 9: Complete Desktop verification and manual Windows acceptance

**Files:**
- Create: `docs/superpowers/verification/2026-09-25-desktop-git-to-host-workflow.md`
- Modify: `docs/ROADMAP.md` only to move actually completed Desktop Git→Host/FTP composition bullets from Active Development to Core Baseline; leave unfinished Terminal/Commit/UI work active.

- [ ] **Step 1: Run full automated matrix on the final SHA**

```powershell
dotnet restore RaayaGitDeploy.slnx
dotnet build RaayaGitDeploy.slnx -c Debug --no-restore
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj -c Debug --no-build
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj -c Debug --no-build
dotnet test tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj -c Debug --no-build
dotnet test tests/RaayaGitDeploy.Android.Core.Tests/RaayaGitDeploy.Android.Core.Tests.csproj -c Debug --no-build
```

Record exact command/output status. Only label `TESTED-PASS` when the command actually exits 0.

- [ ] **Step 2: Manual Windows workflow acceptance**

Run:

```powershell
dotnet run --project src/RaayaGitDeploy.App/RaayaGitDeploy.App.csproj -c Debug --no-build
```

Validate on a disposable Git repo + test host/profile:
1. Open Repository with FolderPicker.
2. Select a profile; no-history state shows explicit baseline choices.
3. Mark baseline A, then make/pull commits B and C without deploy; pending shows A..C.
4. Generated `public/build` files join queue after saving that rule.
5. Deleted and renamed-old paths appear as Delete in Dry Run.
6. Dry Run shows exact repo/profile/A→C and remote mappings.
7. Change HEAD after Dry Run; old Deploy is rejected.
8. Run a failed upload; deletes are blocked and baseline remains A.
9. Run successful deploy; history records A→C and pending becomes empty at C.
10. Test SFTP and FTP/FTPS connection routing. For FTPS, verify an invalid/untrusted certificate fails.
11. Close/reopen app and verify server profile contains only credential reference, while FTP secret remains in Windows Credential Manager.

Mark each item PASS/FAIL/NOT-RUN; never infer manual PASS from CI.

- [ ] **Step 3: Recheck parallel branch before finalizing**

Compare current `feat/git-host-auto-deploy` HEAD against the final branch and document only concrete shared-contract integration points. Do not merge it in this plan.

- [ ] **Step 4: Commit verification/roadmap**

```bash
git add docs/superpowers/verification/2026-09-25-desktop-git-to-host-workflow.md docs/ROADMAP.md
git commit -m "docs(desktop): verify git-to-host deployment workflow"
```

---

## Desktop Acceptance

This plan is complete only when all of the following are true on one green final SHA:

```text
Open repository
→ choose production profile
→ explicit baseline A exists
→ Update/Pull may move HEAD to B then C
→ pending remains A..C
→ pending commits/files are visible
→ queue is auto-built with GitDetected + GeneratedRule while manual items survive
→ Dry Run contains exact profile + A→C + upload/delete operations
→ stale HEAD/profile/queue invalidates old preview
→ failed deploy does not advance A
→ successful deploy records A→C
→ pending at C is empty
→ SFTP routes through SFTP transport
→ FTP routes through FluentFTP without TLS
→ FTPS routes through FluentFTP explicit TLS with certificate validation enabled
→ FTP/FTPS password exists only in Windows Credential Manager, not profile/history
```

## Out of Scope for This Plan

- Mobile companion plan-ID API evolution.
- Actual Android application/platform host and Android Keystore adapter.
- Auto Deploy policy/scheduling integration.
- New Terminal ANSI/VT rendering work.
- Commit graph/hunk selection.
- Automated build-command execution; generated rules include outputs only after they already exist locally.
