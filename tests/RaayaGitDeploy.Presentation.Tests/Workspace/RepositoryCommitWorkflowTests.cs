using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.Presentation.Tests.Workspace;

public sealed class RepositoryCommitWorkflowTests
{
    [Fact]
    public async Task StageAndCommit_RefreshWorkspaceAndExposeNewHead()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeRepositoryService();
        var mutation = new FakeMutationService(repository);
        var workspace = new RepositoryWorkspaceViewModel(repository);
        await workspace.OpenRepositoryAsync("repo", cancellationToken);
        var workflow = new RepositoryCommitWorkflow(mutation, workspace);
        var change = Assert.Single(workspace.Changes);

        await workflow.StageAsync([change], cancellationToken);
        Assert.True(Assert.Single(workspace.Changes).IsStaged);

        var sha = await workflow.CommitAsync("ship real commit", cancellationToken);

        Assert.Equal(FakeMutationService.NewSha, sha);
        Assert.Equal(FakeMutationService.NewSha, workspace.HeadSha);
        Assert.Empty(workspace.Changes);
        Assert.Equal("ship real commit", Assert.Single(workspace.Commits).Subject);
    }

    [Fact]
    public async Task StageAsync_RequiresExplicitSelectionBeforeMutation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeRepositoryService();
        var mutation = new FakeMutationService(repository);
        var workspace = new RepositoryWorkspaceViewModel(repository);
        await workspace.OpenRepositoryAsync("repo", cancellationToken);
        var workflow = new RepositoryCommitWorkflow(mutation, workspace);

        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.StageAsync([], cancellationToken));
        Assert.Equal(0, mutation.StageCalls);
    }

    private sealed class FakeMutationService(FakeRepositoryService repository) : IGitMutationService
    {
        public const string NewSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        public int StageCalls { get; private set; }

        public Task StageAsync(string repositoryPath, IReadOnlyList<string> repositoryRelativePaths, CancellationToken cancellationToken)
        {
            StageCalls++;
            repository.Staged = true;
            return Task.CompletedTask;
        }

        public Task UnstageAsync(string repositoryPath, IReadOnlyList<string> repositoryRelativePaths, CancellationToken cancellationToken)
        {
            repository.Staged = false;
            return Task.CompletedTask;
        }

        public Task<string> CommitStagedAsync(string repositoryPath, string message, CancellationToken cancellationToken)
        {
            repository.Commit(message, NewSha);
            return Task.FromResult(NewSha);
        }
    }

    private sealed class FakeRepositoryService : IGitRepositoryService
    {
        private string _head = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private string _subject = "initial";
        private bool _hasChange = true;
        public bool Staged { get; set; }

        public void Commit(string subject, string sha)
        {
            _subject = subject;
            _head = sha;
            _hasChange = false;
            Staged = false;
        }

        public Task<GitRepositoryContext> GetContextAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(new GitRepositoryContext("repo", "main", _head));

        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitWorkingTreeChange>>(_hasChange ? [new GitWorkingTreeChange("app.txt", GitChangeKind.Modified, null, Staged, !Staged)] : []);

        public Task<IReadOnlyList<GitCommitInfo>> GetRecentCommitsAsync(string repositoryPath, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitCommitInfo>>([new GitCommitInfo(_head, _head[..8], _subject, "Developer", DateTimeOffset.UtcNow)]);

        public Task<IReadOnlyList<GitChange>> GetCommitChangesAsync(string repositoryPath, string commitSha, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GitChange>>([]);
        public Task<string> GetCommitFileDiffAsync(string repositoryPath, string commitSha, string repositoryRelativePath, CancellationToken cancellationToken) => Task.FromResult(string.Empty);
        public Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(string repositoryPath, GitComparisonRequest request, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GitChange>>([]);
        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, CancellationToken cancellationToken) => Task.FromResult(string.Empty);
        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, long maxUntrackedPreviewBytes, CancellationToken cancellationToken) => Task.FromResult(string.Empty);
    }
}
