using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.Presentation.Tests.Deployment;

public sealed class DeploymentRepositorySwitchTests
{
    [Fact]
    public async Task ResetForRepositoryChange_ClearsQueuePreviewAndLastResult()
    {
        var root = Path.Combine(Path.GetTempPath(), "raaya-switch-repository");
        var profile = new ServerProfile("prod", "Production", "example.test", 22, "deploy", "/srv/app", ServerAuthenticationMode.SshKey, "key:prod");
        var queue = new DeploymentQueueViewModel();
        var transport = new FakeTransport();
        var servers = new ServersViewModel(new FakeStore(profile), transport);
        var planner = new DeploymentPlanner();
        var sut = new DeploymentWorkspaceViewModel(queue, servers, new DeploymentDryRunViewModel(planner), planner, new DeploymentExecutor(transport), new FakeHistoryStore());
        await sut.LoadServersAsync(CancellationToken.None);
        queue.AddFile(Path.Combine(root, "app.js"));
        sut.RefreshDryRunPreview(root);
        Assert.NotNull(sut.PreviewPlan);

        sut.ResetForRepositoryChange();

        Assert.Empty(sut.Queue.Items);
        Assert.Null(sut.PreviewPlan);
        Assert.Null(sut.LastResult);
    }

    private sealed class FakeStore(params ServerProfile[] profiles) : IServerProfileStore
    {
        public Task<IReadOnlyList<ServerProfile>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ServerProfile>>(profiles);
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
