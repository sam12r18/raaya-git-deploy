using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.Presentation.Tests.Workspace;

public sealed class RepositoryOpenCoordinatorTests
{
    [Fact]
    public async Task OpenRepositoryAsync_SelectedFolder_LoadsRepository()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var git = new FakeGitRepositoryService();
        var viewModel = new RepositoryWorkspaceViewModel(git);
        var picker = new FakeRepositoryFolderPicker(@"C:\work\repo");
        var coordinator = new RepositoryOpenCoordinator(picker, viewModel);

        await coordinator.OpenRepositoryAsync(cancellationToken);

        Assert.Equal(@"C:\work\repo", git.ContextRequestedPath);
        Assert.Equal(@"C:\work\repo", viewModel.RepositoryPath);
    }

    [Fact]
    public async Task OpenRepositoryAsync_CancelledSelection_DoesNotCallGit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var git = new FakeGitRepositoryService();
        var viewModel = new RepositoryWorkspaceViewModel(git);
        var coordinator = new RepositoryOpenCoordinator(
            new FakeRepositoryFolderPicker(null),
            viewModel);

        await coordinator.OpenRepositoryAsync(cancellationToken);

        Assert.Null(git.ContextRequestedPath);
        Assert.Null(viewModel.RepositoryPath);
    }

    [Fact]
    public async Task OpenRepositoryAsync_PickerFails_ReportsDiagnosticWithoutCallingGit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var git = new FakeGitRepositoryService();
        var viewModel = new RepositoryWorkspaceViewModel(git);
        var coordinator = new RepositoryOpenCoordinator(
            new ThrowingRepositoryFolderPicker(new InvalidOperationException("Folder picker failed to initialize.")),
            viewModel);

        await coordinator.OpenRepositoryAsync(cancellationToken);

        Assert.Null(git.ContextRequestedPath);
        Assert.Equal("Folder picker failed to initialize.", viewModel.ErrorMessage);
    }

    private sealed class FakeRepositoryFolderPicker(string? path) : IRepositoryFolderPicker
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken) =>
            Task.FromResult(path);
    }

    private sealed class ThrowingRepositoryFolderPicker(Exception exception) : IRepositoryFolderPicker
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken) =>
            Task.FromException<string?>(exception);
    }

    private sealed class FakeGitRepositoryService : IGitRepositoryService
    {
        public string? ContextRequestedPath { get; private set; }

        public Task<GitRepositoryContext> GetContextAsync(string path, CancellationToken cancellationToken)
        {
            ContextRequestedPath = path;
            return Task.FromResult(new GitRepositoryContext(path, "main", new string('a', 40)));
        }

        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(string repositoryPath, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitWorkingTreeChange>>([]);

        public Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(string repositoryPath, GitComparisonRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitChange>>([]);

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, long maxTextBytes, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);
    }
}
