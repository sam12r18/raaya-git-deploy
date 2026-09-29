using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.Presentation.Tests.Deployment;

public sealed class DeploymentReviewFreshnessTests
{
    [Fact]
    public async Task Queue_mutation_hides_review_and_disables_deploy_readiness()
    {
        var root = Path.Combine(Path.GetTempPath(), "raaya-review-freshness-queue");
        var profile = Profile("prod", "Production");
        var queue = new DeploymentQueueViewModel();
        var sut = CreateSut(queue, new ServersViewModel(new FakeStore(profile), new FakeTransport()));
        await sut.LoadServersAsync(CancellationToken.None);
        queue.AddFile(Path.Combine(root, "app.js"));

        sut.RefreshDryRunPreview(root);

        Assert.True(sut.CanDeploy);
        Assert.NotNull(sut.PreviewReview);
        queue.AddFile(Path.Combine(root, "style.css"));
        Assert.False(sut.CanDeploy);
        Assert.Null(sut.PreviewReview);
        Assert.Contains("changed after Dry Run", sut.ReviewStatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Server_change_hides_review_and_requires_new_dry_run()
    {
        var root = Path.Combine(Path.GetTempPath(), "raaya-review-freshness-server");
        var production = Profile("prod", "Production");
        var staging = Profile("stage", "Staging");
        var queue = new DeploymentQueueViewModel();
        var servers = new ServersViewModel(new FakeStore(production, staging), new FakeTransport());
        var sut = CreateSut(queue, servers);
        await sut.LoadServersAsync(CancellationToken.None);
        servers.SelectedProfile = production;
        queue.AddFile(Path.Combine(root, "app.js"));
        sut.RefreshDryRunPreview(root);

        servers.SelectedProfile = staging;

        Assert.False(sut.IsReviewCurrent);
        Assert.False(sut.CanDeploy);
        Assert.Null(sut.PreviewReview);
        Assert.Contains("Run Dry Run again", sut.ReviewStatusMessage, StringComparison.Ordinal);
    }

    private static ServerProfile Profile(string id, string name) =>
        new(id, name, $"{id}.example.test", 22, "deploy", "/var/www/app", ServerAuthenticationMode.SshKey, $"key:{id}");

    private static DeploymentWorkspaceViewModel CreateSut(DeploymentQueueViewModel queue, ServersViewModel servers)
    {
        var planner = new DeploymentPlanner();
        return new DeploymentWorkspaceViewModel(queue, servers, new DeploymentDryRunViewModel(planner), planner, new DeploymentExecutor(new FakeTransport()), new FakeHistoryStore());
    }

    private sealed class FakeStore(params ServerProfile[] profiles) : IServerProfileStore
    {
        private readonly List<ServerProfile> _profiles = [.. profiles];
        public Task<IReadOnlyList<ServerProfile>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ServerProfile>>(_profiles);
        public Task UpsertAsync(ServerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeHistoryStore : IDeploymentHistoryStore
    {
        public Task<IReadOnlyList<DeploymentHistoryEntry>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DeploymentHistoryEntry>>([]);
        public Task AppendAsync(DeploymentHistoryEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTransport : IRemoteTransport
    {
        public Task TestConnectionAsync(ServerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task UploadAsync(ServerProfile profile, string localPath, string remotePath, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
