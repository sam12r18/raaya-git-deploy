using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.Presentation.Tests.Workspace;

public sealed class RepositoryWorkspaceStaleRepositoryTests
{
    [Fact]
    public async Task OpenRepositoryAsync_FailedSwitch_PreservesPreviouslyLoadedRepositoryState()
    {
        var cancellationToken = CancellationToken.None;
        var service = new SwitchingGitRepositoryService();
        var viewModel = new RepositoryWorkspaceViewModel(service);

        await viewModel.OpenRepositoryAsync(@"I:\Projects\first", cancellationToken);
        Assert.Equal(@"I:\Projects\first", viewModel.RepositoryPath);
        Assert.Single(viewModel.Changes);

        service.ContextException = new InvalidOperationException("not a git repository");
        await viewModel.OpenRepositoryAsync(@"I:\Projects\broken", cancellationToken);

        Assert.Equal(@"I:\Projects\first", viewModel.RepositoryPath);
        Assert.Equal("main", viewModel.BranchName);
        Assert.Equal("0123456789abcdef0123456789abcdef01234567", viewModel.HeadSha);
        Assert.Null(viewModel.BaseRef);
        Assert.Null(viewModel.SelectedDiffText);
        Assert.Single(viewModel.Changes);
        Assert.Equal("src/App.cs", viewModel.Changes[0].Path);
        Assert.Equal("not a git repository", viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    private sealed class SwitchingGitRepositoryService : IGitRepositoryService
    {
        public Exception? ContextException { get; set; }

        public Task<GitRepositoryContext> GetContextAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ContextException is not null)
            {
                throw ContextException;
            }

            return Task.FromResult(new GitRepositoryContext(
                path,
                "main",
                "0123456789abcdef0123456789abcdef01234567"));
        }

        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<GitWorkingTreeChange> changes =
            [
                new GitWorkingTreeChange(
                    "src/App.cs",
                    GitChangeKind.Modified,
                    null,
                    IsStaged: false,
                    IsUnstaged: true)
            ];
            return Task.FromResult(changes);
        }

        public Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(
            string repositoryPath,
            GitComparisonRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> GetDiffAsync(
            string repositoryPath,
            string path,
            string? baseRef,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> GetDiffAsync(
            string repositoryPath,
            string path,
            string? baseRef,
            long maxUntrackedPreviewBytes,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
