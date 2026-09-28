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
        var profile = new ServerProfile("prod", "Production", "example.com", 22, "deploy", "/var/www/app", ServerAuthenticationMode.Password, null);
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

    private sealed class FakeGitRepositoryService(GitRepositoryContext context) : IGitRepositoryService
    {
        public Task<GitRepositoryContext> GetContextAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult(context);

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
