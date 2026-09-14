using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.Presentation.Tests.Workspace;

public sealed class RepositoryWorkspaceViewModelTests
{
    [Fact]
    public async Task LoadRepositoryAsync_PopulatesContextAndWorkingTreeChanges()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = new StubGitRepositoryService(
            new GitRepositoryContext(
                @"I:\Projects\sample",
                "feature/ai-change",
                "0123456789abcdef0123456789abcdef01234567"),
            new[]
            {
                new GitWorkingTreeChange(
                    "src/App.cs",
                    GitChangeKind.Modified,
                    null,
                    IsStaged: false,
                    IsUnstaged: true),
                new GitWorkingTreeChange(
                    "src/New Service.cs",
                    GitChangeKind.Untracked,
                    null,
                    IsStaged: false,
                    IsUnstaged: true)
            });

        var viewModel = new RepositoryWorkspaceViewModel(service);

        await viewModel.LoadRepositoryAsync(
            @"I:\Projects\sample\src",
            cancellationToken);

        Assert.Equal(@"I:\Projects\sample", viewModel.RepositoryPath);
        Assert.Equal("feature/ai-change", viewModel.BranchName);
        Assert.Equal("0123456789abcdef0123456789abcdef01234567", viewModel.HeadSha);
        Assert.False(viewModel.IsBusy);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Collection(
            viewModel.Changes,
            change =>
            {
                Assert.Equal("src/App.cs", change.Path);
                Assert.Equal(GitChangeKind.Modified, change.Kind);
                Assert.True(change.IsUnstaged);
                Assert.False(change.IsSelectedForDeploy);
            },
            change =>
            {
                Assert.Equal("src/New Service.cs", change.Path);
                Assert.Equal(GitChangeKind.Untracked, change.Kind);
                Assert.True(change.IsUnstaged);
                Assert.False(change.IsSelectedForDeploy);
            });

        Assert.Equal(@"I:\Projects\sample\src", service.ContextRequestedPath);
        Assert.Equal(@"I:\Projects\sample", service.ChangesRequestedPath);
    }

    private sealed class StubGitRepositoryService : IGitRepositoryService
    {
        private readonly GitRepositoryContext _context;
        private readonly IReadOnlyList<GitWorkingTreeChange> _changes;

        public StubGitRepositoryService(
            GitRepositoryContext context,
            IReadOnlyList<GitWorkingTreeChange> changes)
        {
            _context = context;
            _changes = changes;
        }

        public string? ContextRequestedPath { get; private set; }

        public string? ChangesRequestedPath { get; private set; }

        public Task<GitRepositoryContext> GetContextAsync(
            string path,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ContextRequestedPath = path;
            return Task.FromResult(_context);
        }

        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(
            string path,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ChangesRequestedPath = path;
            return Task.FromResult(_changes);
        }

        public Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(
            string repositoryPath,
            GitComparisonRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string> GetDiffAsync(
            string repositoryPath,
            string path,
            string? baseRef,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
