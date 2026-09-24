using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Core.Tests.Deployment;

public sealed class DeploymentRunCoordinatorTests
{
    [Fact]
    public async Task Stale_target_head_is_rejected_before_remote_mutation()
    {
        var git = new FakeGitRepositoryService(@"C:\repo", "main", "dddddddd");
        var transport = new RecordingRemoteTransport();
        var history = new MemoryHistoryStore();
        var coordinator = new DeploymentRunCoordinator(
            git,
            new DeploymentExecutor(transport),
            history);
        var plan = Plan("cccccccc", [Upload("app.php")]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.ExecuteAsync(Profile(), plan, CancellationToken.None));

        Assert.Contains("HEAD", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(transport.Uploaded);
        Assert.Empty(transport.Deleted);
        Assert.Empty(history.Entries);
    }

    [Fact]
    public async Task Failed_upload_blocks_delete_and_does_not_advance_successful_baseline()
    {
        var oldSuccess = SuccessfulHistory("old-success", "aaaaaaaa");
        var history = new MemoryHistoryStore([oldSuccess]);
        var git = new FakeGitRepositoryService(@"C:\repo", "main", "cccccccc");
        var transport = new RecordingRemoteTransport(failUpload: true);
        var coordinator = new DeploymentRunCoordinator(
            git,
            new DeploymentExecutor(transport),
            history);
        var plan = Plan("cccccccc", [Upload("app.php"), Delete("old.php")]);

        var result = await coordinator.ExecuteAsync(Profile(), plan, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Items, item =>
            item.Operation.Kind == DeploymentOperationKind.Upload &&
            item.Status == DeploymentItemStatus.Failed);
        Assert.Contains(result.Items, item =>
            item.Operation.Kind == DeploymentOperationKind.Delete &&
            item.Status == DeploymentItemStatus.Blocked);
        Assert.Empty(transport.Deleted);

        var failedHistory = Assert.Single(history.Entries, entry => entry.Id != "old-success");
        Assert.False(failedHistory.Succeeded);
        Assert.Equal(@"C:\repo", failedHistory.RepositoryPath);
        Assert.Equal("main", failedHistory.Branch);
        Assert.Equal("aaaaaaaa", failedHistory.FromHead);
        Assert.Equal("cccccccc", failedHistory.ToHead);
        Assert.Equal("prod", failedHistory.ServerProfileId);
        Assert.NotNull(failedHistory.FinishedAt);

        var baseline = await new DeploymentBaselineService(history)
            .GetLastSuccessfulAsync(@"C:\repo", "prod", CancellationToken.None);
        Assert.NotNull(baseline);
        Assert.Equal("aaaaaaaa", baseline!.ToHead);
    }

    [Fact]
    public async Task Full_success_records_exact_reviewed_range_and_advances_baseline()
    {
        var oldSuccess = SuccessfulHistory("old-success", "aaaaaaaa");
        var history = new MemoryHistoryStore([oldSuccess]);
        var git = new FakeGitRepositoryService(@"C:\repo", "main", "cccccccc");
        var transport = new RecordingRemoteTransport();
        var coordinator = new DeploymentRunCoordinator(
            git,
            new DeploymentExecutor(transport),
            history);
        var plan = Plan("cccccccc", [Upload("app.php")]);

        var result = await coordinator.ExecuteAsync(Profile(), plan, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Single(transport.Uploaded);
        var successHistory = Assert.Single(history.Entries, entry => entry.Id != "old-success");
        Assert.True(successHistory.Succeeded);
        Assert.Equal(@"C:\repo", successHistory.RepositoryPath);
        Assert.Equal("main", successHistory.Branch);
        Assert.Equal("aaaaaaaa", successHistory.FromHead);
        Assert.Equal("cccccccc", successHistory.ToHead);
        Assert.Equal("prod", successHistory.ServerProfileId);
        Assert.NotNull(successHistory.FinishedAt);

        var baseline = await new DeploymentBaselineService(history)
            .GetLastSuccessfulAsync(@"C:\repo", "prod", CancellationToken.None);
        Assert.NotNull(baseline);
        Assert.Equal("cccccccc", baseline!.ToHead);
    }

    [Fact]
    public async Task Profile_cannot_change_after_plan_review()
    {
        var git = new FakeGitRepositoryService(@"C:\repo", "main", "cccccccc");
        var transport = new RecordingRemoteTransport();
        var coordinator = new DeploymentRunCoordinator(
            git,
            new DeploymentExecutor(transport),
            new MemoryHistoryStore());
        var plan = Plan("cccccccc", [Upload("app.php")]);
        var otherProfile = Profile() with { Id = "staging", DisplayName = "Staging" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.ExecuteAsync(otherProfile, plan, CancellationToken.None));

        Assert.Empty(transport.Uploaded);
    }

    private static DeploymentPlan Plan(
        string targetHead,
        IReadOnlyList<DeploymentOperation> operations) =>
        new(
            operations,
            IsDryRun: false,
            new DeploymentPlanContext(
                @"C:\repo",
                "main",
                "aaaaaaaa",
                targetHead,
                "prod"));

    private static DeploymentOperation Upload(string relativePath) =>
        new(
            DeploymentOperationKind.Upload,
            $@"C:\repo\{relativePath}",
            $"/public_html/{relativePath}");

    private static DeploymentOperation Delete(string relativePath) =>
        new(
            DeploymentOperationKind.Delete,
            $@"C:\repo\{relativePath}",
            $"/public_html/{relativePath}");

    private static ServerProfile Profile() =>
        new(
            "prod",
            "Production",
            "example.invalid",
            22,
            "deploy",
            "/public_html",
            ServerAuthenticationMode.SshKey,
            "key-ref",
            ServerTransportKind.Sftp);

    private static DeploymentHistoryEntry SuccessfulHistory(string id, string toHead)
    {
        var started = DateTimeOffset.Parse("2026-09-24T10:00:00Z");
        return new DeploymentHistoryEntry(
            id,
            started,
            "prod",
            "Production",
            true,
            [],
            @"C:\repo",
            "main",
            null,
            toHead,
            started.AddMinutes(1));
    }

    private sealed class MemoryHistoryStore : IDeploymentHistoryStore
    {
        public MemoryHistoryStore(IEnumerable<DeploymentHistoryEntry>? entries = null) =>
            Entries = entries?.ToList() ?? [];

        public List<DeploymentHistoryEntry> Entries { get; }

        public Task<IReadOnlyList<DeploymentHistoryEntry>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DeploymentHistoryEntry>>(Entries.ToArray());

        public Task AppendAsync(DeploymentHistoryEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGitRepositoryService(
        string rootPath,
        string branch,
        string head) : IGitRepositoryService
    {
        public Task<GitRepositoryContext> GetContextAsync(
            string path,
            CancellationToken cancellationToken) =>
            Task.FromResult(new GitRepositoryContext(rootPath, branch, head));

        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(
            string path,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

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

    private sealed class RecordingRemoteTransport(bool failUpload = false) : IRemoteTransport
    {
        public List<(string LocalPath, string RemotePath)> Uploaded { get; } = [];
        public List<string> Deleted { get; } = [];

        public Task TestConnectionAsync(ServerProfile profile, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> ListAsync(
            ServerProfile profile,
            string remotePath,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task UploadAsync(
            ServerProfile profile,
            string localPath,
            string remotePath,
            CancellationToken cancellationToken)
        {
            Uploaded.Add((localPath, remotePath));
            if (failUpload)
                throw new InvalidOperationException("simulated upload failure");
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            ServerProfile profile,
            string remotePath,
            CancellationToken cancellationToken)
        {
            Deleted.Add(remotePath);
            return Task.CompletedTask;
        }
    }
}
