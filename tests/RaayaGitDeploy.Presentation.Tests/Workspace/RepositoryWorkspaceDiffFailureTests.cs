using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.Presentation.Tests.Workspace;

public sealed class RepositoryWorkspaceDiffFailureTests
{
    [Fact]
    public async Task LoadDiffAsync_WhenNextDiffFails_ClearsPreviouslyDisplayedDiff()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = new DiffFailureGitRepositoryService();
        var viewModel = new RepositoryWorkspaceViewModel(service);

        await viewModel.OpenRepositoryAsync(@"I:\Projects\sample", cancellationToken);
        var change = Assert.Single(viewModel.Changes);

        service.DiffText = "@@ -1 +1 @@\n-old\n+new";
        await viewModel.LoadDiffAsync(change, cancellationToken);
        Assert.Equal(service.DiffText, viewModel.SelectedDiffText);

        service.DiffException = new InvalidOperationException("diff failed");
        await viewModel.LoadDiffAsync(change, cancellationToken);

        Assert.Null(viewModel.SelectedDiffText);
        Assert.Equal("diff failed", viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    private sealed class DiffFailureGitRepositoryService : IGitRepositoryService
    {
        public string DiffText { get; set; } = string.Empty;
        public Exception? DiffException { get; set; }

        public Task<GitRepositoryContext> GetContextAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new GitRepositoryContext(
                @"I:\Projects\sample",
                "main",
                "0123456789abcdef0123456789abcdef01234567"));
        }

        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(
            string path,
            CancellationToken cancellationToken)
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
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<GitChange>>([]);
        }

        public Task<string> GetDiffAsync(
            string repositoryPath,
            string path,
            string? baseRef,
            CancellationToken cancellationToken) =>
            GetDiffAsync(repositoryPath, path, baseRef, RepositoryWorkspaceViewModel.MaxTextPreviewBytes, cancellationToken);

        public Task<string> GetDiffAsync(
            string repositoryPath,
            string path,
            string? baseRef,
            long maxUntrackedPreviewBytes,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (DiffException is not null)
            {
                throw DiffException;
            }

            return Task.FromResult(DiffText);
        }
    }
}
