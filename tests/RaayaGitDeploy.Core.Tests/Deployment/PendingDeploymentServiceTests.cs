using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Core.Tests.Deployment;

public sealed class PendingDeploymentServiceTests
{
    [Fact]
    public async Task Multiple_updates_still_use_last_deployed_head_as_range_start()
    {
        var git = FakeGitRepositoryService.AtHead("cccccccc");
        git.CommitsBetween =
        [
            new("cccccccc", "cccccccc", "C", "Dev", DateTimeOffset.UtcNow),
            new("bbbbbbbb", "bbbbbbbb", "B", "Dev", DateTimeOffset.UtcNow)
        ];
        git.ChangesBetween = [new("app.php", GitChangeKind.Modified)];

        var history = new MemoryHistoryStore(
        [
            new DeploymentHistoryEntry(
                "deploy-a",
                DateTimeOffset.UtcNow,
                "prod",
                "Production",
                true,
                [],
                @"C:\repo",
                "main",
                null,
                "aaaaaaaa")
        ]);

        var service = new PendingDeploymentService(git, new DeploymentBaselineService(history));
        var snapshot = await service.BuildAsync(@"C:\repo", "prod", CancellationToken.None);

        Assert.False(snapshot.RequiresBaseline);
        Assert.Equal("aaaaaaaa", snapshot.FromHead);
        Assert.Equal("cccccccc", snapshot.ToHead);
        Assert.Equal("main", snapshot.Branch);
        Assert.Equal("aaaaaaaa", git.RequestedBase);
        Assert.Equal("cccccccc", git.RequestedTarget);
        Assert.Equal(2, snapshot.Commits.Count);
        Assert.Single(snapshot.Changes);
        Assert.False(git.WorkingTreeRequested);
    }

    [Fact]
    public async Task Missing_git_aware_baseline_returns_baseline_required_without_guessing()
    {
        var git = FakeGitRepositoryService.AtHead("cccccccc");
        var service = new PendingDeploymentService(
            git,
            new DeploymentBaselineService(new MemoryHistoryStore([])));

        var snapshot = await service.BuildAsync(@"C:\repo", "prod", CancellationToken.None);

        Assert.True(snapshot.RequiresBaseline);
        Assert.Null(snapshot.FromHead);
        Assert.Equal("cccccccc", snapshot.ToHead);
        Assert.Empty(snapshot.Commits);
        Assert.Empty(snapshot.Changes);
        Assert.Null(git.RequestedBase);
        Assert.Null(git.RequestedTarget);
        Assert.False(git.WorkingTreeRequested);
    }

    [Fact]
    public async Task Already_deployed_head_returns_empty_pending_range_without_range_queries()
    {
        var git = FakeGitRepositoryService.AtHead("aaaaaaaa");
        var history = new MemoryHistoryStore(
        [
            new DeploymentHistoryEntry(
                "deploy-a",
                DateTimeOffset.UtcNow,
                "prod",
                "Production",
                true,
                [],
                @"C:\repo",
                "main",
                null,
                "aaaaaaaa")
        ]);

        var service = new PendingDeploymentService(git, new DeploymentBaselineService(history));
        var snapshot = await service.BuildAsync(@"C:\repo", "prod", CancellationToken.None);

        Assert.False(snapshot.RequiresBaseline);
        Assert.Equal("aaaaaaaa", snapshot.FromHead);
        Assert.Equal("aaaaaaaa", snapshot.ToHead);
        Assert.Empty(snapshot.Commits);
        Assert.Empty(snapshot.Changes);
        Assert.Null(git.RequestedBase);
        Assert.Null(git.RequestedTarget);
    }

    private sealed class MemoryHistoryStore(IReadOnlyList<DeploymentHistoryEntry> entries) : IDeploymentHistoryStore
    {
        public Task<IReadOnlyList<DeploymentHistoryEntry>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(entries);

        public Task AppendAsync(DeploymentHistoryEntry entry, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeGitRepositoryService : IGitRepositoryService
    {
        private FakeGitRepositoryService(GitRepositoryContext context) => Context = context;

        public GitRepositoryContext Context { get; }
        public IReadOnlyList<GitCommitInfo> CommitsBetween { get; set; } = [];
        public IReadOnlyList<GitChange> ChangesBetween { get; set; } = [];
        public string? RequestedBase { get; private set; }
        public string? RequestedTarget { get; private set; }
        public bool WorkingTreeRequested { get; private set; }

        public static FakeGitRepositoryService AtHead(string headSha) =>
            new(new GitRepositoryContext(@"C:\repo", "main", headSha));

        public Task<GitRepositoryContext> GetContextAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(Context);

        public Task<IReadOnlyList<GitCommitInfo>> GetCommitsBetweenAsync(
            string repositoryPath,
            string baseRef,
            string targetRef,
            CancellationToken cancellationToken)
        {
            RequestedBase = baseRef;
            RequestedTarget = targetRef;
            return Task.FromResult(CommitsBetween);
        }

        public Task<IReadOnlyList<GitChange>> GetChangesBetweenAsync(
            string repositoryPath,
            string baseRef,
            string targetRef,
            CancellationToken cancellationToken)
        {
            Assert.Equal(RequestedBase, baseRef);
            Assert.Equal(RequestedTarget, targetRef);
            return Task.FromResult(ChangesBetween);
        }

        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(
            string path,
            CancellationToken cancellationToken)
        {
            WorkingTreeRequested = true;
            throw new InvalidOperationException("Pending deployment must not inspect working-tree changes.");
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
