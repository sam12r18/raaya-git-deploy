using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.Presentation.Tests.Workspace;

public sealed class RepositoryWorkspaceNavigationTests
{
    [Fact]
    public void NewWorkspace_DefaultsToChangesSection()
    {
        var viewModel = new RepositoryWorkspaceViewModel(new StubGitRepositoryService());

        Assert.Equal(WorkspaceSection.Changes, viewModel.SelectedSection);
    }

    [Theory]
    [InlineData(WorkspaceSection.Commits)]
    [InlineData(WorkspaceSection.Terminal)]
    [InlineData(WorkspaceSection.Commands)]
    [InlineData(WorkspaceSection.DeployQueue)]
    [InlineData(WorkspaceSection.Servers)]
    [InlineData(WorkspaceSection.History)]
    public async Task SelectSection_PreservesRepositoryContext(WorkspaceSection section)
    {
        var viewModel = new RepositoryWorkspaceViewModel(new StubGitRepositoryService());
        await viewModel.OpenRepositoryAsync(@"I:\Projects\sample", TestContext.Current.CancellationToken);

        viewModel.SelectedSection = section;

        Assert.Equal(section, viewModel.SelectedSection);
        Assert.Equal(@"I:\Projects\sample", viewModel.RepositoryPath);
        Assert.Equal("main", viewModel.BranchName);
        Assert.Equal("0123456789abcdef0123456789abcdef01234567", viewModel.HeadSha);
    }

    private sealed class StubGitRepositoryService : IGitRepositoryService
    {
        public Task<GitRepositoryContext> GetContextAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(new GitRepositoryContext(
                @"I:\Projects\sample",
                "main",
                "0123456789abcdef0123456789abcdef01234567"));

        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitWorkingTreeChange>>([]);

        public Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(string repositoryPath, GitComparisonRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitChange>>([]);

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, long maxUntrackedPreviewBytes, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);
    }
}
