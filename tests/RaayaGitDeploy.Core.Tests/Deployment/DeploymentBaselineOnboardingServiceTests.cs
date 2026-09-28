using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Core.Tests.Deployment;

public sealed class DeploymentBaselineOnboardingServiceTests
{
    [Fact]
    public async Task MarkCurrentHeadAsync_AppendsAuditedBaselineEvent()
    {
        var history = new MemoryHistoryStore();
        var git = new FakeGitRepositoryService(new GitRepositoryContext(@"C:\work\app", "main", "cccccccc"));
        var profile = CreateProfile();
        var service = new DeploymentBaselineOnboardingService(git, history);

        var entry = await service.MarkCurrentHeadAsync(@"C:\work\app", profile, CancellationToken.None);

        Assert.Equal(DeploymentHistoryEventKind.BaselineMarked, entry.EventKind);
        Assert.True(entry.Succeeded);
        Assert.Empty(entry.Items);
        Assert.Equal(@"C:\work\app", entry.RepositoryPath);
        Assert.Equal("main", entry.Branch);
        Assert.Equal("cccccccc", entry.ToHead);
        Assert.Equal("prod", entry.ServerProfileId);
        Assert.Equal("Production", entry.ServerDisplayName);
        Assert.NotNull(entry.FinishedAt);
        Assert.Single(history.Entries);
        Assert.Same(entry, history.Entries[0]);
    }

    [Fact]
    public async Task MarkRecentCommitAsync_AppendsSelectedReachableCommitAsBaseline()
    {
        var history = new MemoryHistoryStore();
        var git = new FakeGitRepositoryService(
            new GitRepositoryContext(@"C:\work\app", "main", "cccccccc"),
            [Commit("bbbbbbbb")]);
        var service = new DeploymentBaselineOnboardingService(git, history);

        var entry = await service.MarkRecentCommitAsync(@"C:\work\app", CreateProfile(), "bbbbbbbb", CancellationToken.None);

        Assert.Equal(DeploymentHistoryEventKind.BaselineMarked, entry.EventKind);
        Assert.Equal("bbbbbbbb", entry.ToHead);
        Assert.Single(history.Entries);
    }

    [Fact]
    public async Task MarkRecentCommitAsync_RejectsCommitOutsideReachableRecentHistoryWithoutWritingHistory()
    {
        var history = new MemoryHistoryStore();
        var git = new FakeGitRepositoryService(
            new GitRepositoryContext(@"C:\work\app", "main", "cccccccc"),
            [Commit("bbbbbbbb")]);
        var service = new DeploymentBaselineOnboardingService(git, history);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.MarkRecentCommitAsync(@"C:\work\app", CreateProfile(), "foreignsha", CancellationToken.None));

        Assert.Contains("reachable", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(history.Entries);
    }

    private static GitCommitInfo Commit(string sha) => new(
        sha,
        sha[..Math.Min(7, sha.Length)],
        "Previous deploy",
        "Dev",
        DateTimeOffset.UtcNow.AddHours(-1));

    private static ServerProfile CreateProfile() => new(
        "prod",
        "Production",
        "example.com",
        22,
        "deploy",
        "/var/www/app",
        ServerAuthenticationMode.ExternalCredentialReference,
        "cred:prod");

    private sealed class MemoryHistoryStore : IDeploymentHistoryStore
    {
        public List<DeploymentHistoryEntry> Entries { get; } = [];

        public Task<IReadOnlyList<DeploymentHistoryEntry>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DeploymentHistoryEntry>>(Entries);

        public Task AppendAsync(DeploymentHistoryEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGitRepositoryService(
        GitRepositoryContext context,
        IReadOnlyList<GitCommitInfo>? recentCommits = null) : IGitRepositoryService
    {
        public Task<GitRepositoryContext> GetContextAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(context);

        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitWorkingTreeChange>>([]);

        public Task<IReadOnlyList<GitCommitInfo>> GetRecentCommitsAsync(string repositoryPath, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(recentCommits ?? (IReadOnlyList<GitCommitInfo>)[]);

        public Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(string repositoryPath, GitComparisonRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitChange>>([]);

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, long maxUntrackedPreviewBytes, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);
    }
}
