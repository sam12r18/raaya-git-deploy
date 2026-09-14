# Foundation & Git Review Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first working Raaya Git Deploy desktop slice: open a local Git repository, show branch/HEAD and machine-parsed changes, compare against a commit/branch, inspect file diffs, and record independent review/deploy-selection state.

**Architecture:** Keep Git semantics in Core contracts and parsers, invoke real `git.exe` only from Infrastructure, keep testable MVVM state in a Presentation project, and use the WinUI 3 App project as a thin Windows shell. This slice intentionally stops before terminal and remote deployment so it can be reviewed, tested, and accepted independently.

**Tech Stack:** .NET 10, WinUI 3, Windows App SDK, CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection, xUnit, `git.exe`

**Spec:** `docs/architecture/PRODUCT_ARCHITECTURE.md`

## Global Constraints

- Target Windows with .NET 10.
- Use WinUI 3 / Windows App SDK for the desktop shell.
- Use installed `git.exe` as the Git source of truth.
- Parse machine-readable Git output; do not parse localized human-readable status text.
- Keep Core independent of WinUI, SSH.NET, SQLite, WebView2, and ConPTY implementation details.
- Keep review state independent from deployment selection state.
- The primary workflow is Review → Verify → Deploy, with human control retained for AI-generated changes.
- Use a dark-first, dense, developer-oriented UI.
- Do not store credentials or secrets in this slice.
- Use TDD for domain/parsing behavior and integration tests for real Git CLI behavior.
- Commit after each independently testable task.

---

## Planned File Structure

```text
RaayaGitDeploy.sln

src/
  RaayaGitDeploy.App/
    App.xaml
    App.xaml.cs
    MainWindow.xaml
    MainWindow.xaml.cs
    Bootstrap/ServiceRegistration.cs
    Views/RepositoryWorkspacePage.xaml
    Views/RepositoryWorkspacePage.xaml.cs

  RaayaGitDeploy.Core/
    Git/GitChangeKind.cs
    Git/GitChange.cs
    Git/GitRepositoryContext.cs
    Git/GitComparisonRequest.cs
    Git/IGitRepositoryService.cs
    Git/Parsing/GitPorcelainV2Parser.cs
    Git/Parsing/GitNameStatusParser.cs
    Review/ReviewState.cs
    Review/ReviewItem.cs
    Review/ReviewSession.cs

  RaayaGitDeploy.Infrastructure/
    GitCli/GitCommandResult.cs
    GitCli/IGitProcessRunner.cs
    GitCli/GitProcessRunner.cs
    GitCli/GitRepositoryService.cs

  RaayaGitDeploy.Presentation/
    Workspace/RepositoryWorkspaceViewModel.cs
    Workspace/ChangeItemViewModel.cs

tests/
  RaayaGitDeploy.Core.Tests/
    Git/GitPorcelainV2ParserTests.cs
    Git/GitNameStatusParserTests.cs
    Review/ReviewSessionTests.cs

  RaayaGitDeploy.Infrastructure.Tests/
    GitCli/GitProcessRunnerTests.cs
    GitCli/GitRepositoryServiceTests.cs

  RaayaGitDeploy.Presentation.Tests/
    Workspace/RepositoryWorkspaceViewModelTests.cs
```

---

### Task 1: Scaffold the Solution and Project Boundaries

**Files:**
- Create: `RaayaGitDeploy.sln`
- Create: `src/RaayaGitDeploy.App/RaayaGitDeploy.App.csproj`
- Create: `src/RaayaGitDeploy.Core/RaayaGitDeploy.Core.csproj`
- Create: `src/RaayaGitDeploy.Infrastructure/RaayaGitDeploy.Infrastructure.csproj`
- Create: `src/RaayaGitDeploy.Presentation/RaayaGitDeploy.Presentation.csproj`
- Create: `tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj`
- Create: `tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj`
- Create: `tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj`
- Create: `.gitignore`

**Interfaces:**
- Consumes: none
- Produces: four production project boundaries and three test projects used by every later task

- [ ] **Step 1: Create solution and class-library/test projects**

Run from the repository root:

```powershell
dotnet new sln -n RaayaGitDeploy
dotnet new classlib -n RaayaGitDeploy.Core -f net10.0 -o src/RaayaGitDeploy.Core
dotnet new classlib -n RaayaGitDeploy.Infrastructure -f net10.0 -o src/RaayaGitDeploy.Infrastructure
dotnet new classlib -n RaayaGitDeploy.Presentation -f net10.0 -o src/RaayaGitDeploy.Presentation
dotnet new xunit -n RaayaGitDeploy.Core.Tests -f net10.0 -o tests/RaayaGitDeploy.Core.Tests
dotnet new xunit -n RaayaGitDeploy.Infrastructure.Tests -f net10.0 -o tests/RaayaGitDeploy.Infrastructure.Tests
dotnet new xunit -n RaayaGitDeploy.Presentation.Tests -f net10.0 -o tests/RaayaGitDeploy.Presentation.Tests
```

Create the WinUI 3 app using the installed Windows App SDK / WinUI project template, naming the project `RaayaGitDeploy.App` and placing it at `src/RaayaGitDeploy.App`.

- [ ] **Step 2: Add projects to the solution**

```powershell
dotnet sln RaayaGitDeploy.sln add `
  src/RaayaGitDeploy.App/RaayaGitDeploy.App.csproj `
  src/RaayaGitDeploy.Core/RaayaGitDeploy.Core.csproj `
  src/RaayaGitDeploy.Infrastructure/RaayaGitDeploy.Infrastructure.csproj `
  src/RaayaGitDeploy.Presentation/RaayaGitDeploy.Presentation.csproj `
  tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj `
  tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj `
  tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj
```

- [ ] **Step 3: Add project references**

```powershell
dotnet add src/RaayaGitDeploy.Infrastructure/RaayaGitDeploy.Infrastructure.csproj reference src/RaayaGitDeploy.Core/RaayaGitDeploy.Core.csproj
dotnet add src/RaayaGitDeploy.Presentation/RaayaGitDeploy.Presentation.csproj reference src/RaayaGitDeploy.Core/RaayaGitDeploy.Core.csproj
dotnet add src/RaayaGitDeploy.App/RaayaGitDeploy.App.csproj reference src/RaayaGitDeploy.Core/RaayaGitDeploy.Core.csproj
dotnet add src/RaayaGitDeploy.App/RaayaGitDeploy.App.csproj reference src/RaayaGitDeploy.Infrastructure/RaayaGitDeploy.Infrastructure.csproj
dotnet add src/RaayaGitDeploy.App/RaayaGitDeploy.App.csproj reference src/RaayaGitDeploy.Presentation/RaayaGitDeploy.Presentation.csproj
dotnet add tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj reference src/RaayaGitDeploy.Core/RaayaGitDeploy.Core.csproj
dotnet add tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj reference src/RaayaGitDeploy.Core/RaayaGitDeploy.Core.csproj
dotnet add tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj reference src/RaayaGitDeploy.Infrastructure/RaayaGitDeploy.Infrastructure.csproj
dotnet add tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj reference src/RaayaGitDeploy.Core/RaayaGitDeploy.Core.csproj
dotnet add tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj reference src/RaayaGitDeploy.Presentation/RaayaGitDeploy.Presentation.csproj
```

- [ ] **Step 4: Add required MVVM/DI packages**

```powershell
dotnet add src/RaayaGitDeploy.Presentation/RaayaGitDeploy.Presentation.csproj package CommunityToolkit.Mvvm
dotnet add src/RaayaGitDeploy.App/RaayaGitDeploy.App.csproj package Microsoft.Extensions.DependencyInjection
```

- [ ] **Step 5: Delete template `Class1.cs` files and verify the solution builds**

```powershell
Remove-Item src/RaayaGitDeploy.Core/Class1.cs
Remove-Item src/RaayaGitDeploy.Infrastructure/Class1.cs
Remove-Item src/RaayaGitDeploy.Presentation/Class1.cs
dotnet build RaayaGitDeploy.sln
dotnet test RaayaGitDeploy.sln
```

Expected: build succeeds and all template tests pass.

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "build: scaffold Raaya Git Deploy solution"
```

---

### Task 2: Define Git Domain Contracts and Real Process Execution

**Files:**
- Create: `src/RaayaGitDeploy.Infrastructure/GitCli/GitCommandResult.cs`
- Create: `src/RaayaGitDeploy.Infrastructure/GitCli/IGitProcessRunner.cs`
- Create: `src/RaayaGitDeploy.Infrastructure/GitCli/GitProcessRunner.cs`
- Test: `tests/RaayaGitDeploy.Infrastructure.Tests/GitCli/GitProcessRunnerTests.cs`

**Interfaces:**
- Consumes: .NET `Process`
- Produces:
  - `Task<GitCommandResult> IGitProcessRunner.RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)`
  - `GitCommandResult(int ExitCode, string StandardOutput, string StandardError)`

- [ ] **Step 1: Write the failing integration test**

```csharp
using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_GitVersion_ReturnsSuccessfulResult()
    {
        var runner = new GitProcessRunner();

        var result = await runner.RunAsync(
            Directory.GetCurrentDirectory(),
            new[] { "--version" },
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("git version ", result.StandardOutput.Trim());
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```powershell
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj --filter GitProcessRunnerTests
```

Expected: FAIL because `GitProcessRunner` and related types do not exist.

- [ ] **Step 3: Implement the command result and process-runner contract**

```csharp
namespace RaayaGitDeploy.Infrastructure.GitCli;

public sealed record GitCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

public interface IGitProcessRunner
{
    Task<GitCommandResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Implement `GitProcessRunner` using `ArgumentList`**

```csharp
using System.Diagnostics;

namespace RaayaGitDeploy.Infrastructure.GitCli;

public sealed class GitProcessRunner : IGitProcessRunner
{
    public async Task<GitCommandResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git.exe",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return new GitCommandResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

```powershell
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj --filter GitProcessRunnerTests
```

Expected: PASS on a development machine with Git installed.

- [ ] **Step 6: Commit**

```bash
git add src/RaayaGitDeploy.Infrastructure tests/RaayaGitDeploy.Infrastructure.Tests
git commit -m "feat: add git process runner"
```

---

### Task 3: Resolve Repository Root, Branch, and HEAD

**Files:**
- Create: `src/RaayaGitDeploy.Core/Git/GitRepositoryContext.cs`
- Create: `src/RaayaGitDeploy.Core/Git/IGitRepositoryService.cs`
- Create: `src/RaayaGitDeploy.Infrastructure/GitCli/GitRepositoryService.cs`
- Test: `tests/RaayaGitDeploy.Infrastructure.Tests/GitCli/GitRepositoryServiceTests.cs`

**Interfaces:**
- Consumes: `IGitProcessRunner.RunAsync(...)`
- Produces:
  - `Task<GitRepositoryContext> IGitRepositoryService.GetContextAsync(string path, CancellationToken cancellationToken)`
  - `GitRepositoryContext(string RootPath, string BranchName, string HeadSha)`

- [ ] **Step 1: Write a failing integration test using a temporary Git repository**

```csharp
[Fact]
public async Task GetContextAsync_ReturnsRootBranchAndHead()
{
    var root = Path.Combine(Path.GetTempPath(), $"rgd-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);

    try
    {
        var runner = new GitProcessRunner();
        await runner.RunAsync(root, new[] { "init", "-b", "main" }, CancellationToken.None);
        await runner.RunAsync(root, new[] { "config", "user.email", "test@example.invalid" }, CancellationToken.None);
        await runner.RunAsync(root, new[] { "config", "user.name", "Raaya Test" }, CancellationToken.None);

        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# test");
        await runner.RunAsync(root, new[] { "add", "README.md" }, CancellationToken.None);
        await runner.RunAsync(root, new[] { "commit", "-m", "initial" }, CancellationToken.None);

        var service = new GitRepositoryService(runner);

        var context = await service.GetContextAsync(root, CancellationToken.None);

        Assert.Equal(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar), context.RootPath.TrimEnd(Path.DirectorySeparatorChar));
        Assert.Equal("main", context.BranchName);
        Assert.Matches("^[0-9a-f]{40}$", context.HeadSha);
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}
```

- [ ] **Step 2: Run the test and verify failure**

```powershell
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj --filter GetContextAsync_ReturnsRootBranchAndHead
```

Expected: FAIL because repository service contracts do not exist.

- [ ] **Step 3: Add the Core context record and service contract**

```csharp
namespace RaayaGitDeploy.Core.Git;

public sealed record GitRepositoryContext(
    string RootPath,
    string BranchName,
    string HeadSha);

public interface IGitRepositoryService
{
    Task<GitRepositoryContext> GetContextAsync(
        string path,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Implement repository discovery**

Use three machine-stable commands:

```text
git rev-parse --show-toplevel
git branch --show-current
git rev-parse HEAD
```

Throw `InvalidOperationException` with the Git stderr when a required command exits non-zero. If branch output is empty, use the short HEAD SHA prefixed with `detached@` as the displayed branch label.

- [ ] **Step 5: Run the test suite**

```powershell
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/RaayaGitDeploy.Core src/RaayaGitDeploy.Infrastructure tests/RaayaGitDeploy.Infrastructure.Tests
git commit -m "feat: resolve git repository context"
```

---

### Task 4: Parse Working Tree Changes from Porcelain v2

**Files:**
- Create: `src/RaayaGitDeploy.Core/Git/GitChangeKind.cs`
- Create: `src/RaayaGitDeploy.Core/Git/GitChange.cs`
- Create: `src/RaayaGitDeploy.Core/Git/Parsing/GitPorcelainV2Parser.cs`
- Modify: `src/RaayaGitDeploy.Core/Git/IGitRepositoryService.cs`
- Modify: `src/RaayaGitDeploy.Infrastructure/GitCli/GitRepositoryService.cs`
- Test: `tests/RaayaGitDeploy.Core.Tests/Git/GitPorcelainV2ParserTests.cs`
- Test: `tests/RaayaGitDeploy.Infrastructure.Tests/GitCli/GitRepositoryServiceTests.cs`

**Interfaces:**
- Consumes: `git status --porcelain=v2 -z --untracked-files=all`
- Produces:
  - `enum GitChangeKind { Added, Modified, Deleted, Renamed, Untracked }`
  - `GitChange(string Path, GitChangeKind Kind, string? OriginalPath = null)`
  - `Task<IReadOnlyList<GitChange>> GetWorkingTreeChangesAsync(...)`

- [ ] **Step 1: Write failing parser tests**

Cover at minimum:

```csharp
[Theory]
[InlineData("? new.txt\0", "new.txt", GitChangeKind.Untracked)]
[InlineData("1 .M N... 100644 100644 100644 abcdef0 abcdef0 src/app.cs\0", "src/app.cs", GitChangeKind.Modified)]
[InlineData("1 D. N... 100644 000000 000000 abcdef0 0000000 old.txt\0", "old.txt", GitChangeKind.Deleted)]
public void Parse_RecognizesSinglePathEntries(
    string payload,
    string expectedPath,
    GitChangeKind expectedKind)
{
    var changes = GitPorcelainV2Parser.Parse(payload);

    var change = Assert.Single(changes);
    Assert.Equal(expectedPath, change.Path);
    Assert.Equal(expectedKind, change.Kind);
}
```

Add a dedicated rename test with a type-2 record and its second NUL-delimited original path.

- [ ] **Step 2: Run the tests and verify failure**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj --filter GitPorcelainV2ParserTests
```

Expected: FAIL because the parser does not exist.

- [ ] **Step 3: Implement enum, record, and parser**

Rules:

```text
?  → Untracked
1 A* or *A → Added
1 M* or *M → Modified
1 D* or *D → Deleted
2 ...      → Renamed, with OriginalPath populated
```

Ignore porcelain header records beginning with `# `.

Reject unsupported non-empty record types with `FormatException`; do not silently reinterpret unknown formats.

- [ ] **Step 4: Add `GetWorkingTreeChangesAsync` to `IGitRepositoryService` and implementation**

Run:

```text
git status --porcelain=v2 -z --untracked-files=all
```

Parse stdout with `GitPorcelainV2Parser.Parse`.

- [ ] **Step 5: Add an integration test that creates modified, deleted, and untracked files**

Create a temp repository, commit two files, modify one, delete one, create one untracked file, call `GetWorkingTreeChangesAsync`, and assert all three states.

- [ ] **Step 6: Run Core and Infrastructure tests**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj
dotnet test tests/RaayaGitDeploy.Infrastructure.Tests/RaayaGitDeploy.Infrastructure.Tests.csproj
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src tests
git commit -m "feat: discover working tree changes"
```

---

### Task 5: Compare Changes Since a Commit or Branch and Read File Diff

**Files:**
- Create: `src/RaayaGitDeploy.Core/Git/GitComparisonRequest.cs`
- Create: `src/RaayaGitDeploy.Core/Git/Parsing/GitNameStatusParser.cs`
- Modify: `src/RaayaGitDeploy.Core/Git/IGitRepositoryService.cs`
- Modify: `src/RaayaGitDeploy.Infrastructure/GitCli/GitRepositoryService.cs`
- Test: `tests/RaayaGitDeploy.Core.Tests/Git/GitNameStatusParserTests.cs`
- Test: `tests/RaayaGitDeploy.Infrastructure.Tests/GitCli/GitRepositoryServiceTests.cs`

**Interfaces:**
- Consumes: a user-selected Git ref
- Produces:
  - `GitComparisonRequest(string BaseRef)`
  - `Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(string repositoryPath, GitComparisonRequest request, CancellationToken cancellationToken)`
  - `Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, CancellationToken cancellationToken)`

- [ ] **Step 1: Write failing name-status parser tests**

Use NUL-delimited examples for:

```text
A\0src/new.cs\0
M\0src/app.cs\0
D\0src/old.cs\0
R100\0src/old-name.cs\0src/new-name.cs\0
```

Assert rename maps to:

```csharp
new GitChange("src/new-name.cs", GitChangeKind.Renamed, "src/old-name.cs")
```

- [ ] **Step 2: Run tests and verify failure**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj --filter GitNameStatusParserTests
```

- [ ] **Step 3: Implement `GitNameStatusParser`**

Recognize `A`, `M`, `D`, and `R<score>` tokens. Use the second path of a rename as the current path and the first as `OriginalPath`.

- [ ] **Step 4: Implement comparison and diff service methods**

For comparison:

```text
git diff --name-status -M -z <baseRef>...HEAD
```

For an individual diff with a base ref:

```text
git diff --no-color <baseRef>...HEAD -- <path>
```

For a working-tree diff without a base ref:

```text
git diff --no-color HEAD -- <path>
```

If an untracked working-tree file produces no Git diff, read its text content and render it as an “all added” preview only when it is a text file and below the UI preview size limit defined by Presentation.

- [ ] **Step 5: Add an integration test with two commits**

Create commit A, create commit B that modifies/renames files, compare `HEAD~1` to `HEAD`, and assert the change list.

- [ ] **Step 6: Run all tests**

```powershell
dotnet test RaayaGitDeploy.sln
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src tests
git commit -m "feat: compare git refs and load file diffs"
```

---

### Task 6: Add Review Session Domain State

**Files:**
- Create: `src/RaayaGitDeploy.Core/Review/ReviewState.cs`
- Create: `src/RaayaGitDeploy.Core/Review/ReviewItem.cs`
- Create: `src/RaayaGitDeploy.Core/Review/ReviewSession.cs`
- Test: `tests/RaayaGitDeploy.Core.Tests/Review/ReviewSessionTests.cs`

**Interfaces:**
- Consumes: `IReadOnlyList<GitChange>`
- Produces:
  - `enum ReviewState { Unreviewed, Approved, Excluded }`
  - `ReviewItem`
  - `ReviewSession`
  - independent `IsSelectedForDeployment`

- [ ] **Step 1: Write failing tests for independent review/deploy state**

```csharp
[Fact]
public void ApprovingItem_DoesNotAutomaticallySelectItForDeployment()
{
    var session = ReviewSession.Create(new[]
    {
        new GitChange("src/app.cs", GitChangeKind.Modified)
    });

    session.SetReviewState("src/app.cs", ReviewState.Approved);

    var item = Assert.Single(session.Items);
    Assert.Equal(ReviewState.Approved, item.ReviewState);
    Assert.False(item.IsSelectedForDeployment);
}
```

Also test:

- selecting an unreviewed item is allowed in domain state,
- excluded item becomes unselected,
- unknown path mutation throws `KeyNotFoundException`.

- [ ] **Step 2: Run tests and verify failure**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj --filter ReviewSessionTests
```

- [ ] **Step 3: Implement the review model**

`ReviewSession.SetReviewState(path, ReviewState.Excluded)` must force `IsSelectedForDeployment = false`.

`ReviewSession.SetDeploymentSelected(path, true)` must not mutate the review state.

- [ ] **Step 4: Run tests**

```powershell
dotnet test tests/RaayaGitDeploy.Core.Tests/RaayaGitDeploy.Core.Tests.csproj
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/RaayaGitDeploy.Core tests/RaayaGitDeploy.Core.Tests
git commit -m "feat: add git review session model"
```

---

### Task 7: Build the Testable Repository Workspace ViewModel

**Files:**
- Create: `src/RaayaGitDeploy.Presentation/Workspace/ChangeItemViewModel.cs`
- Create: `src/RaayaGitDeploy.Presentation/Workspace/RepositoryWorkspaceViewModel.cs`
- Test: `tests/RaayaGitDeploy.Presentation.Tests/Workspace/RepositoryWorkspaceViewModelTests.cs`

**Interfaces:**
- Consumes: `IGitRepositoryService`
- Produces:
  - `Task OpenRepositoryAsync(string path, CancellationToken cancellationToken)`
  - `Task CompareSinceAsync(string baseRef, CancellationToken cancellationToken)`
  - `Task LoadDiffAsync(ChangeItemViewModel item, CancellationToken cancellationToken)`
  - observable `RepositoryPath`, `BranchName`, `HeadSha`, `Changes`, `SelectedDiffText`, `IsBusy`, `ErrorMessage`

- [ ] **Step 1: Write a fake repository service and failing view-model test**

Test opening a repository:

```csharp
[Fact]
public async Task OpenRepositoryAsync_LoadsContextAndWorkingTree()
{
    var service = new FakeGitRepositoryService
    {
        Context = new GitRepositoryContext(@"C:\repo", "main", new string('a', 40)),
        WorkingTreeChanges =
        [
            new GitChange("src/app.cs", GitChangeKind.Modified)
        ]
    };

    var vm = new RepositoryWorkspaceViewModel(service);

    await vm.OpenRepositoryAsync(@"C:\repo", CancellationToken.None);

    Assert.Equal("main", vm.BranchName);
    Assert.Equal(new string('a', 40), vm.HeadSha);
    Assert.Single(vm.Changes);
}
```

- [ ] **Step 2: Run tests and verify failure**

```powershell
dotnet test tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj
```

- [ ] **Step 3: Implement observable view models using CommunityToolkit.Mvvm**

`ChangeItemViewModel` exposes:

```text
Path
OriginalPath
Kind
ReviewState
IsSelectedForDeployment
```

`RepositoryWorkspaceViewModel` owns the current review session and maps domain items to observable UI items.

- [ ] **Step 4: Add tests for compare mode and diff loading**

Assert that:

- compare mode replaces the displayed change collection,
- loading a diff stores the text in `SelectedDiffText`,
- a Git exception populates `ErrorMessage` and resets `IsBusy`.

- [ ] **Step 5: Run Presentation tests**

```powershell
dotnet test tests/RaayaGitDeploy.Presentation.Tests/RaayaGitDeploy.Presentation.Tests.csproj
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/RaayaGitDeploy.Presentation tests/RaayaGitDeploy.Presentation.Tests
git commit -m "feat: add repository workspace presentation model"
```

---

### Task 8: Build the WinUI 3 Git Review Workspace

**Files:**
- Create/Modify: `src/RaayaGitDeploy.App/App.xaml`
- Create/Modify: `src/RaayaGitDeploy.App/App.xaml.cs`
- Create/Modify: `src/RaayaGitDeploy.App/MainWindow.xaml`
- Create/Modify: `src/RaayaGitDeploy.App/MainWindow.xaml.cs`
- Create: `src/RaayaGitDeploy.App/Bootstrap/ServiceRegistration.cs`
- Create: `src/RaayaGitDeploy.App/Views/RepositoryWorkspacePage.xaml`
- Create: `src/RaayaGitDeploy.App/Views/RepositoryWorkspacePage.xaml.cs`

**Interfaces:**
- Consumes: `RepositoryWorkspaceViewModel`, `IGitRepositoryService`, `GitRepositoryService`, `IGitProcessRunner`, `GitProcessRunner`
- Produces: the first runnable desktop workspace

- [ ] **Step 1: Register application services**

Register singleton/stateless services:

```csharp
services.AddSingleton<IGitProcessRunner, GitProcessRunner>();
services.AddSingleton<IGitRepositoryService, GitRepositoryService>();
services.AddTransient<RepositoryWorkspaceViewModel>();
```

- [ ] **Step 2: Build the dark developer-oriented shell**

Main layout:

```text
┌───────────────────────────────────────────────────────────┐
│ Raaya Git Deploy          repo / branch / HEAD            │
├────────────┬──────────────────────────────┬───────────────┤
│ Navigation │ Changes                      │ Inspector      │
│            │ status + review + deploy     │ Diff           │
│            │ selection                    │                │
└────────────┴──────────────────────────────┴───────────────┘
```

Use WinUI theme resources rather than hard-coded per-control colors. Default the application to dark theme while preserving a future theme-setting path.

- [ ] **Step 3: Add repository selection**

Use `FolderPicker` with the WinUI window handle initialized correctly.

After selection call:

```csharp
await ViewModel.OpenRepositoryAsync(folder.Path, CancellationToken.None);
```

Show repository root, branch, and abbreviated HEAD in the workspace header.

- [ ] **Step 4: Render the change list**

Each row must expose:

- change-kind badge,
- path,
- rename original path when present,
- review-state selector,
- deploy-selection checkbox.

Do not use checkbox state as a synonym for review approval.

- [ ] **Step 5: Add Working Tree / Since Ref controls**

Provide:

- `Working Tree` action,
- base-ref input,
- `Compare` action.

Comparison invokes `CompareSinceAsync`.

- [ ] **Step 6: Add the first diff inspector**

Selecting a file invokes `LoadDiffAsync`.

Render `SelectedDiffText` in a monospaced, read-only viewer with horizontal scrolling. Do not introduce WebView2 in this task; the advanced diff renderer remains isolated for a later improvement and must not block the first working Git review slice.

- [ ] **Step 7: Add busy/error states**

While a Git operation is running:

- show a progress indicator,
- prevent duplicate invocation,
- keep already loaded data visible.

Show `ErrorMessage` in a non-destructive InfoBar.

- [ ] **Step 8: Build and manually verify the app**

```powershell
dotnet build RaayaGitDeploy.sln
dotnet test RaayaGitDeploy.sln
```

Manual acceptance:

1. Open a real Git repository.
2. Confirm repository root, branch, and HEAD.
3. Modify a tracked file.
4. Create an untracked file.
5. Confirm both appear.
6. Mark one Approved without selecting it for deployment.
7. Select another file for deployment while it remains Unreviewed.
8. Load a diff.
9. Compare against `HEAD~1`.
10. Confirm UI remains responsive during Git execution.

- [ ] **Step 9: Commit**

```bash
git add src tests
git commit -m "feat: add git review workspace"
```

---

## Plan Acceptance Criteria

This plan is complete when all of the following are true:

- The solution builds on the supported Windows development environment.
- All automated tests pass.
- A user can choose a local Git repository.
- Branch and HEAD are displayed.
- Working-tree Added/Modified/Deleted/Renamed/Untracked changes are machine-parsed.
- A user can compare current HEAD against a commit/branch reference.
- A user can inspect a text diff.
- Review state is independent from deployment selection.
- No remote deployment or credential functionality has been introduced yet.
- The codebase preserves Core / Infrastructure / Presentation / App boundaries.

## Follow-on Plans

After this slice is accepted, implementation continues with separate detailed plans in this order:

1. **Integrated Terminal & Saved Commands** — ConPTY session lifecycle, terminal host UI, repository-aware shell, saved commands.
2. **Deployment Planning & SFTP** — mapping, queue, server profiles, DPAPI secrets, SSH host trust, dry run, upload/delete safety.
3. **Deployment History & Product Hardening** — deployment snapshots, structured logs, recovery UX, packaging, contributor-facing quality gates.
