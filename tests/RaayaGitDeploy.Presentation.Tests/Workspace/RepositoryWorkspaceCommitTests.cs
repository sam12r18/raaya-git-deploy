using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.Presentation.Tests.Workspace;

public sealed class RepositoryWorkspaceCommitTests
{
    [Fact]
    public async Task OpenRepositoryAsync_LoadsRecentCommitsAndSelectCommitLoadsFiles()
    {
        var service = new CommitGitService();
        var viewModel = new RepositoryWorkspaceViewModel(service);

        await viewModel.OpenRepositoryAsync(@"I:\Projects\sample", TestContext.Current.CancellationToken);

        var commit = Assert.Single(viewModel.Commits);
        Assert.Equal("12345678", commit.ShortSha);

        await viewModel.SelectCommitAsync(commit, TestContext.Current.CancellationToken);

        Assert.Same(commit, viewModel.SelectedCommit);
        Assert.Equal("src/App.cs", Assert.Single(viewModel.CommitChanges).Path);
        Assert.Null(viewModel.SelectedCommitFile);
        Assert.Null(viewModel.SelectedCommitDiffText);
    }

    [Fact]
    public async Task SelectingCommitAndFile_ClearsStaleStateWhenNextLoadFails()
    {
        var service = new CommitGitService();
        var viewModel = new RepositoryWorkspaceViewModel(service);
        await viewModel.OpenRepositoryAsync(@"I:\Projects\sample", TestContext.Current.CancellationToken);
        var commit = Assert.Single(viewModel.Commits);
        await viewModel.SelectCommitAsync(commit, TestContext.Current.CancellationToken);
        var file = Assert.Single(viewModel.CommitChanges);

        await viewModel.SelectCommitFileAsync(file, TestContext.Current.CancellationToken);
        Assert.Equal("commit diff", viewModel.SelectedCommitDiffText);

        service.CommitChangesException = new InvalidOperationException("commit files failed");
        await viewModel.SelectCommitAsync(commit, TestContext.Current.CancellationToken);

        Assert.Empty(viewModel.CommitChanges);
        Assert.Null(viewModel.SelectedCommitFile);
        Assert.Null(viewModel.SelectedCommitDiffText);
        Assert.Equal("commit files failed", viewModel.ErrorMessage);
    }

    private sealed class CommitGitService : IGitRepositoryService
    {
        private readonly GitCommitInfo _commit = new(
            "1234567890abcdef1234567890abcdef12345678",
            "12345678",
            "Workbench commit",
            "Developer",
            DateTimeOffset.Parse("2026-09-17T12:00:00+00:00"));

        public Exception? CommitChangesException { get; set; }

        public Task<GitRepositoryContext> GetContextAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(new GitRepositoryContext(@"I:\Projects\sample", "feature/workbench", _commit.Sha));

        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitWorkingTreeChange>>([]);

        public Task<IReadOnlyList<GitCommitInfo>> GetRecentCommitsAsync(string repositoryPath, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitCommitInfo>>([_commit]);

        public Task<IReadOnlyList<GitChange>> GetCommitChangesAsync(string repositoryPath, string commitSha, CancellationToken cancellationToken)
        {
            if (CommitChangesException is not null) throw CommitChangesException;
            return Task.FromResult<IReadOnlyList<GitChange>>([new GitChange("src/App.cs", GitChangeKind.Modified)]);
        }

        public Task<string> GetCommitFileDiffAsync(string repositoryPath, string commitSha, string repositoryRelativePath, CancellationToken cancellationToken) =>
            Task.FromResult("commit diff");

        public Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(string repositoryPath, GitComparisonRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitChange>>([]);

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, long maxUntrackedPreviewBytes, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);
    }
}
