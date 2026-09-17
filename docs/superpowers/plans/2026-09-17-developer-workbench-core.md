# Developer Workbench Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn Raaya Git Deploy from the current Git Review foundation into a usable desktop developer workbench with navigation, commit review, integrated terminal, saved commands, deployment planning, servers, and history.

**Architecture:** Preserve Core / Infrastructure / Presentation / App boundaries. Platform-neutral contracts and models stay testable outside WinUI; Git, ConPTY, persistence and SFTP implementations live in Infrastructure; Presentation owns workspace state; App only wires WinUI/platform-specific controls.

**Tech Stack:** .NET 10, C# 14, WinUI 3 / Windows App SDK, MVVM, git.exe, Windows ConPTY, PowerShell, SSH.NET, local JSON/SQLite persistence as appropriate.

**Spec:** `docs/superpowers/specs/2026-09-17-developer-workbench-core-design.md`

## Global Constraints

- Target Windows desktop; current App target remains `net10.0-windows10.0.26100.0`, x64.
- Do not execute repository content automatically; terminal/commands require explicit user action.
- Credentials must never be committed or written to ordinary structured logs.
- Git arguments remain structured; user-controlled refs must not be interpreted as Git options.
- Long-running Git, terminal and network work must not block the WinUI thread.
- A test may only be recorded as passing after it was actually executed successfully.
- Keep commits cohesive: each commit should contain a meaningful related slice, not one trivial edit.

---

### Task 1: Workspace shell and navigation

**Files:**
- Modify: `src/RaayaGitDeploy.App/Views/RepositoryWorkspaceView.xaml`
- Modify: `src/RaayaGitDeploy.Presentation/RepositoryWorkspaceViewModel.cs`
- Test: `tests/RaayaGitDeploy.Presentation.Tests/RepositoryWorkspaceViewModelTests.cs`

**Interfaces:**
- Produces: `WorkspaceSection` enum and `SelectedSection` state with Changes, Commits, Terminal, Commands, DeployQueue, Servers, History.

- [ ] Write Presentation tests proving default section is Changes and navigation changes only workspace section without clearing repository context.
- [ ] Run the focused Presentation tests and record the actual RED result.
- [ ] Implement `WorkspaceSection`, selection state and commands with no Git side effects.
- [ ] Run focused tests, then full Presentation tests; record actual results.
- [ ] Replace the single-page layout with persistent repository header + left navigation + section host. Preserve existing Changes/Diff functionality under Changes.
- [ ] Build `RaayaGitDeploy.slnx`; only mark build passing if execution succeeds.
- [ ] Commit the cohesive shell slice.

### Task 2: Recent commits domain and Git parsing

**Files:**
- Create: `src/RaayaGitDeploy.Core/Git/GitCommitInfo.cs`
- Modify: `src/RaayaGitDeploy.Core/Git/IGitRepositoryService.cs`
- Modify: `src/RaayaGitDeploy.Infrastructure/Git/GitCliRepositoryService.cs`
- Test: `tests/RaayaGitDeploy.Infrastructure.Tests/GitCliRepositoryServiceTests.cs`

**Interfaces:**
- Produces: `Task<IReadOnlyList<GitCommitInfo>> GetRecentCommitsAsync(string repositoryPath, int limit, CancellationToken cancellationToken)`.
- `GitCommitInfo`: full SHA, short SHA, subject, author name, author date.

- [ ] Add failing tests for parsing multiple commits including Unicode author/subject and delimiter-safe output.
- [ ] Run focused Infrastructure tests and record RED.
- [ ] Implement Git log using an explicit machine-readable delimiter format and validate `limit` within 1..200.
- [ ] Run focused tests and full Infrastructure suite.
- [ ] Commit commit-history service slice.

### Task 3: Commit files and commit diff

**Files:**
- Modify: `src/RaayaGitDeploy.Core/Git/IGitRepositoryService.cs`
- Modify: `src/RaayaGitDeploy.Infrastructure/Git/GitCliRepositoryService.cs`
- Test: `tests/RaayaGitDeploy.Infrastructure.Tests/GitCliRepositoryServiceTests.cs`

**Interfaces:**
- Produces: `GetCommitChangesAsync(repositoryPath, commitSha, cancellationToken)` returning existing changed-file model.
- Produces: `GetCommitFileDiffAsync(repositoryPath, commitSha, repositoryRelativePath, cancellationToken)` returning diff text/result model used by the existing inspector.

- [ ] Write failing tests for added/modified/deleted/renamed files in a commit and safe SHA/path handling.
- [ ] Run focused tests and record RED.
- [ ] Implement minimal Git commands with machine-readable name-status parsing and repository-relative path validation.
- [ ] Run focused + Infrastructure suites.
- [ ] Commit commit-detail slice.

### Task 4: Commits workspace UI

**Files:**
- Modify: `src/RaayaGitDeploy.Presentation/RepositoryWorkspaceViewModel.cs`
- Modify: `src/RaayaGitDeploy.App/Views/RepositoryWorkspaceView.xaml`
- Test: `tests/RaayaGitDeploy.Presentation.Tests/RepositoryWorkspaceViewModelTests.cs`

**Interfaces:**
- Consumes Tasks 2–3 commit APIs.
- Produces selected commit, commit changes, selected commit file and commit diff state.

- [ ] Add failing ViewModel tests for repository load, commit selection, stale-state clearing and failure recovery.
- [ ] Run focused Presentation tests and record RED.
- [ ] Implement commit state/commands and cancellation-safe loading.
- [ ] Run Presentation suite.
- [ ] Build a master/detail Commits page: commit list → changed files → diff inspector.
- [ ] Build solution and commit the complete commits-workspace slice.

### Task 5: Terminal abstraction and ConPTY session

**Files:**
- Create: `src/RaayaGitDeploy.Core/Terminal/ITerminalSession.cs`
- Create: `src/RaayaGitDeploy.Core/Terminal/ITerminalSessionFactory.cs`
- Create: `src/RaayaGitDeploy.Core/Terminal/TerminalSessionState.cs`
- Create: `src/RaayaGitDeploy.Infrastructure/Terminal/ConPtyTerminalSession.cs`
- Create: `src/RaayaGitDeploy.Infrastructure/Terminal/ConPtyTerminalSessionFactory.cs`
- Test: `tests/RaayaGitDeploy.Infrastructure.Tests/ConPtyTerminalSessionTests.cs`

**Interfaces:**
- `ITerminalSession.StartAsync(string workingDirectory, CancellationToken)`.
- `ITerminalSession.WriteAsync(string input, CancellationToken)`.
- `ITerminalSession.ResizeAsync(int columns, int rows, CancellationToken)`.
- `ITerminalSession.StopAsync(CancellationToken)`.
- Output/event stream exposes text chunks and exited state.

- [ ] Write tests for lifecycle/state validation and factory working-directory propagation; isolate Windows-native boundary behind injectable adapter where needed.
- [ ] Run focused tests and record RED.
- [ ] Implement ConPTY lifecycle with PowerShell as initial shell, asynchronous read/write and deterministic handle/process cleanup.
- [ ] Run Infrastructure tests on Windows CI.
- [ ] Commit terminal infrastructure slice.

### Task 6: Terminal workspace UI

**Files:**
- Create: `src/RaayaGitDeploy.Presentation/Terminal/TerminalViewModel.cs`
- Create: `src/RaayaGitDeploy.App/Views/TerminalView.xaml`
- Create: `src/RaayaGitDeploy.App/Views/TerminalView.xaml.cs`
- Modify: `src/RaayaGitDeploy.App/Views/RepositoryWorkspaceView.xaml`
- Test: `tests/RaayaGitDeploy.Presentation.Tests/TerminalViewModelTests.cs`

**Interfaces:**
- Consumes `ITerminalSessionFactory`.
- Produces terminal output, input, running/exited state, start/stop commands and repository-aware working directory.

- [ ] Write failing ViewModel tests for start, output streaming, input, stop and repository switch behavior.
- [ ] Run focused tests and record RED.
- [ ] Implement testable TerminalViewModel.
- [ ] Run Presentation suite.
- [ ] Implement terminal surface with monospace output, input handling, start/restart/stop and resize forwarding.
- [ ] Build solution; commit terminal UI slice.
- [ ] Record Windows manual acceptance separately: interactive PowerShell prompt, `git status`, resize, Ctrl+C/stop, repository working directory. Do not mark these passing from CI alone.

### Task 7: Saved Commands

**Files:**
- Create: `src/RaayaGitDeploy.Core/Commands/SavedCommand.cs`
- Create: `src/RaayaGitDeploy.Core/Commands/ISavedCommandStore.cs`
- Create: `src/RaayaGitDeploy.Infrastructure/Commands/JsonSavedCommandStore.cs`
- Create: `src/RaayaGitDeploy.Presentation/Commands/CommandsViewModel.cs`
- Modify: `src/RaayaGitDeploy.App/Views/RepositoryWorkspaceView.xaml`
- Test: `tests/RaayaGitDeploy.Infrastructure.Tests/JsonSavedCommandStoreTests.cs`
- Test: `tests/RaayaGitDeploy.Presentation.Tests/CommandsViewModelTests.cs`

**Interfaces:**
- `SavedCommand`: id, name, command text, optional working-directory override.
- Store supports load/upsert/delete.
- Commands ViewModel runs selected command only after explicit user action through terminal execution boundary.

- [ ] Write failing persistence and ViewModel tests, including malformed local file recovery and no implicit execution.
- [ ] Run tests to establish RED.
- [ ] Implement local persistence outside repository and command execution orchestration.
- [ ] Run Infrastructure + Presentation suites.
- [ ] Implement Commands page with add/edit/delete/run and useful starter empty state.
- [ ] Build and commit cohesive Saved Commands slice.

### Task 8: Deploy Queue planning

**Files:**
- Create: `src/RaayaGitDeploy.Core/Deployment/DeploymentQueueItem.cs`
- Create: `src/RaayaGitDeploy.Presentation/Deployment/DeploymentQueueViewModel.cs`
- Modify: `src/RaayaGitDeploy.Presentation/RepositoryWorkspaceViewModel.cs`
- Modify: `src/RaayaGitDeploy.App/Views/RepositoryWorkspaceView.xaml`
- Test: `tests/RaayaGitDeploy.Presentation.Tests/DeploymentQueueViewModelTests.cs`

**Interfaces:**
- Queue item records local path, source (`GitSelection`, `ManualFile`, `ManualFolder`) and future remote mapping field.

- [ ] Write failing tests proving deploy selection feeds queue without changing review state and duplicate paths normalize to one queue item.
- [ ] Run focused tests and record RED.
- [ ] Implement queue composition and explicit Add File/Add Folder hooks.
- [ ] Run Presentation tests.
- [ ] Implement Queue page with source badges, remove/clear actions and mapping placeholder fields that do not deploy yet.
- [ ] Build and commit queue slice.

### Task 9: Server profiles and transport boundary

**Files:**
- Create: `src/RaayaGitDeploy.Core/Deployment/ServerProfile.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/IRemoteTransport.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/IServerProfileStore.cs`
- Create: `src/RaayaGitDeploy.Infrastructure/Deployment/JsonServerProfileStore.cs`
- Create: `src/RaayaGitDeploy.Presentation/Deployment/ServersViewModel.cs`
- Modify: `src/RaayaGitDeploy.App/Views/RepositoryWorkspaceView.xaml`
- Test: corresponding Infrastructure and Presentation test files.

**Interfaces:**
- Profile includes id, display name, host, port, username, remote root, auth mode and non-secret key reference; no plaintext password field in persisted profile.
- `IRemoteTransport` defines connect/test/list/upload/delete primitives with cancellation.

- [ ] Write failing tests for profile validation/persistence and absence of plaintext secret persistence.
- [ ] Run RED tests.
- [ ] Implement profile store and transport contract without remote operations yet.
- [ ] Run tests.
- [ ] Implement Servers CRUD UI and connection configuration surface.
- [ ] Build and commit server-profile slice.

### Task 10: SFTP transport, mapping preview and Dry Run

**Files:**
- Create: `src/RaayaGitDeploy.Infrastructure/Deployment/SftpRemoteTransport.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/DeploymentPlan.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/DeploymentPlanner.cs`
- Modify Queue/Servers Presentation and App files.
- Test: Core planner tests + Infrastructure transport tests using a fake/adapter boundary rather than real production credentials.

**Interfaces:**
- Planner maps queue local paths under repository root to normalized paths under configured remote root.
- Dry Run returns planned upload/delete/skip operations without mutating remote state.

- [ ] Write failing containment, traversal, mapping and dry-run tests.
- [ ] Run RED tests.
- [ ] Implement planner and SFTP adapter with host-key verification boundary.
- [ ] Run Core/Infrastructure suites.
- [ ] Add mapping preview and Dry Run UI; no destructive operation without explicit confirmation.
- [ ] Build and commit dry-run slice.

### Task 11: Deployment execution safety and History

**Files:**
- Create: `src/RaayaGitDeploy.Core/Deployment/DeploymentResult.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/DeploymentExecutor.cs`
- Create: `src/RaayaGitDeploy.Core/Deployment/IDeploymentHistoryStore.cs`
- Create: Infrastructure local history store.
- Create/modify Presentation History ViewModel and App History view.
- Test: Core execution safety tests, Infrastructure persistence tests, Presentation history tests.

**Interfaces:**
- Executor performs uploads before deletes, blocks delete phase after required upload failure, supports cancellation and emits per-item results.
- History records repository, branch, HEAD, timestamp, target profile and per-item outcome.

- [ ] Write failing tests for upload-before-delete, delete blocking, cancellation and result recording.
- [ ] Run RED tests.
- [ ] Implement executor and local history store.
- [ ] Run automated suites.
- [ ] Implement explicit deploy confirmation and History page.
- [ ] Build solution and commit deployment/history slice.

### Task 12: Full verification and documentation

**Files:**
- Modify: `docs/LOCAL_DEVELOPMENT.fa.md`
- Create/update: `docs/superpowers/verification/2026-09-17-developer-workbench-core.md`
- Modify: `docs/ROADMAP.md` only to reflect capabilities actually implemented.

- [ ] Run `dotnet restore RaayaGitDeploy.slnx`.
- [ ] Run `dotnet build RaayaGitDeploy.slnx -c Debug --no-restore`.
- [ ] Run Core, Infrastructure and Presentation test projects independently and capture actual counts/results.
- [ ] Verify GitHub Actions for the exact HEAD SHA and record run id/conclusion.
- [ ] Perform/record Windows manual acceptance for navigation, repository switching, commits, diff, terminal, saved commands, queue, servers, dry run, deploy confirmation and history. Any item not actually exercised remains UNTESTED.
- [ ] Update local-development guide with final startup and smoke-test instructions.
- [ ] Commit verification/docs as one cohesive documentation commit.
