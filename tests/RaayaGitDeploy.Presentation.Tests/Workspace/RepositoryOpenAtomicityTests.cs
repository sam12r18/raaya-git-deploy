using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.Presentation.Tests.Workspace;

public sealed class RepositoryOpenAtomicityTests
{
    [Fact]
    public async Task OpenRepositoryAsync_FailedSwitch_PreservesCurrentRepositoryState()
    {
        var cancellationToken = CancellationToken.None;
        var git = new SwitchingGitRepositoryService();
        var viewModel = new RepositoryWorkspaceViewModel(git);

        await viewModel.OpenRepositoryAsync(@"C:\work\repo-a", cancellationToken);
        Assert.Equal(@"C:\work\repo-a", viewModel.RepositoryPath);
        Assert.Single(viewModel.Changes);

        await viewModel.OpenRepositoryAsync(@"C:\work\not-a-repo", cancellationToken);

        Assert.Equal(@"C:\work\repo-a", viewModel.RepositoryPath);
        Assert.Equal("main", viewModel.BranchName);
        Assert.Single(viewModel.Changes);
        Assert.Equal("src/app.cs", viewModel.Changes[0].Path);
        Assert.Equal("Selected folder is not a Git repository.", viewModel.ErrorMessage);
    }

    private sealed class SwitchingGitRepositoryService : IGitRepositoryService
    {
        public Task<GitRepositoryContext> GetContextAsync(string path, CancellationToken cancellationToken)
        {
            if (path.EndsWith("not-a-repo", StringComparison.Ordinal))
                throw new InvalidOperationException("Selected folder is not a Git repository.");

            return Task.FromResult(new GitRepositoryContext(path, "main", new string('a', 40)));
        }

        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(string repositoryPath, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitWorkingTreeChange>>([
                new GitWorkingTreeChange("src/app.cs", GitChangeKind.Modified, OriginalPath: null, IsStaged: false, IsUnstaged: true)
            ]);

        public Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(string repositoryPath, GitComparisonRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitChange>>([]);

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, long maxTextBytes, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);
    }
}
