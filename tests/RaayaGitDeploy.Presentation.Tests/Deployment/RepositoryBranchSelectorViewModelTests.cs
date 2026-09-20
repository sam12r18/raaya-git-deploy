using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.Presentation.Tests.Deployment;

public sealed class RepositoryBranchSelectorViewModelTests
{
    [Fact]
    public async Task RefreshAsync_fetches_remote_and_exposes_revision_difference()
    {
        var repositories = new FakeRepositoryService(new GitRepositoryContext("C:/repo", "main", "local-sha"));
        var tracking = new FakeRemoteTrackingService("remote-sha");
        var viewModel = new RepositoryBranchSelectorViewModel(repositories, tracking)
        {
            RepositoryPath = "C:/repo",
            RemoteName = "origin",
            BranchName = "main"
        };

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(RepositoryTrackingState.Success, viewModel.State);
        Assert.Equal("local-sha", viewModel.LocalRevision);
        Assert.Equal("remote-sha", viewModel.RemoteRevision);
        Assert.True(viewModel.HasRevisionDifference);
        Assert.Equal(("C:/repo", "origin"), tracking.FetchRequest);
        Assert.Equal(("C:/repo", "origin", "main"), tracking.RevisionRequest);
    }

    [Theory]
    [InlineData("--upload-pack=evil")]
    [InlineData("feature bad")]
    [InlineData("feature..bad")]
    public async Task RefreshAsync_rejects_unsafe_git_names(string unsafeName)
    {
        var tracking = new FakeRemoteTrackingService("remote-sha");
        var viewModel = new RepositoryBranchSelectorViewModel(
            new FakeRepositoryService(new GitRepositoryContext("C:/repo", "main", "local-sha")),
            tracking)
        {
            RepositoryPath = "C:/repo",
            RemoteName = unsafeName,
            BranchName = "main"
        };

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(RepositoryTrackingState.Error, viewModel.State);
        Assert.Null(tracking.FetchRequest);
    }

    private sealed class FakeRemoteTrackingService(string revision) : IGitRemoteTrackingService
    {
        public (string RepositoryPath, string RemoteName)? FetchRequest { get; private set; }
        public (string RepositoryPath, string RemoteName, string BranchName)? RevisionRequest { get; private set; }

        public Task FetchAsync(string repositoryPath, string remoteName, CancellationToken cancellationToken)
        {
            FetchRequest = (repositoryPath, remoteName);
            return Task.CompletedTask;
        }

        public Task<string> GetRemoteRevisionAsync(string repositoryPath, string remoteName, string branchName, CancellationToken cancellationToken)
        {
            RevisionRequest = (repositoryPath, remoteName, branchName);
            return Task.FromResult(revision);
        }
    }

    private sealed class FakeRepositoryService(GitRepositoryContext context) : IGitRepositoryService
    {
        public Task<GitRepositoryContext> GetContextAsync(string path, CancellationToken cancellationToken) => Task.FromResult(context);
        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(string path, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GitWorkingTreeChange>>([]);
        public Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(string repositoryPath, GitComparisonRequest request, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GitChange>>([]);
        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, CancellationToken cancellationToken) => Task.FromResult(string.Empty);
        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, long maxUntrackedPreviewBytes, CancellationToken cancellationToken) => Task.FromResult(string.Empty);
    }
}
