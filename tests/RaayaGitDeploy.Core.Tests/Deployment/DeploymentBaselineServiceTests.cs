using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Core.Tests.Deployment;

public sealed class DeploymentBaselineServiceTests
{
    private sealed class MemoryHistoryStore(IReadOnlyList<DeploymentHistoryEntry> entries) : IDeploymentHistoryStore
    {
        public Task<IReadOnlyList<DeploymentHistoryEntry>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(entries);

        public Task AppendAsync(DeploymentHistoryEntry entry, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static DeploymentHistoryEntry Entry(
        string id,
        bool succeeded,
        string repositoryPath,
        string profileId,
        string toHead,
        DateTimeOffset startedAt) =>
        new(
            id,
            startedAt,
            profileId,
            "Production",
            succeeded,
            [],
            repositoryPath,
            "main",
            "base",
            toHead,
            startedAt.AddMinutes(1));

    [Fact]
    public async Task Returns_latest_successful_git_entry_for_equivalent_windows_path()
    {
        var store = new MemoryHistoryStore([
            Entry("1", true, @"C:\work\app", "prod", "aaa", DateTimeOffset.Parse("2026-09-24T10:00:00Z")),
            Entry("2", false, @"C:\work\app", "prod", "bbb", DateTimeOffset.Parse("2026-09-24T11:00:00Z")),
            Entry("3", true, @"c:\WORK\app", "prod", "ccc", DateTimeOffset.Parse("2026-09-24T12:00:00Z"))
        ]);

        var baseline = await new DeploymentBaselineService(store)
            .GetLastSuccessfulAsync(@"C:\work\app", "prod", CancellationToken.None);

        Assert.NotNull(baseline);
        Assert.Equal("ccc", baseline!.ToHead);
    }

    [Fact]
    public async Task Legacy_and_failed_entries_do_not_form_git_baseline()
    {
        var store = new MemoryHistoryStore([
            new DeploymentHistoryEntry("legacy", DateTimeOffset.UtcNow, "prod", "Production", true, []),
            Entry("failed", false, @"C:\work\app", "prod", "bbb", DateTimeOffset.UtcNow)
        ]);

        var baseline = await new DeploymentBaselineService(store)
            .GetLastSuccessfulAsync(@"C:\work\app", "prod", CancellationToken.None);

        Assert.Null(baseline);
    }
}
