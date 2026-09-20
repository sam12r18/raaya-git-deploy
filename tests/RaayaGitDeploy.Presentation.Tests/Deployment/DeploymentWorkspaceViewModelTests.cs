using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.Presentation.Tests.Deployment;

public sealed class DeploymentWorkspaceViewModelTests
{
    [Fact]
    public async Task Preview_Uses_Shared_Queue_And_Selected_Server()
    {
        var root = Path.Combine(Path.GetTempPath(), "raaya-workbench-preview");
        var localPath = Path.Combine(root, "src", "App.cs");
        var profile = new ServerProfile("prod", "Production", "example.test", 22, "deploy", "/var/www/app", ServerAuthenticationMode.SshKey, "key:prod");
        var store = new FakeStore(profile);
        var queue = new DeploymentQueueViewModel();
        var transport = new FakeTransport();
        var servers = new ServersViewModel(store, transport);
        var sut = CreateSut(queue, servers, transport);

        await sut.LoadServersAsync(CancellationToken.None);
        queue.AddFile(localPath);

        var plan = sut.CreateDryRunPreview(root);

        Assert.True(plan.IsDryRun);
        var operation = Assert.Single(plan.Operations);
        Assert.Equal("/var/www/app/src/App.cs", operation.RemotePath);
    }

    [Fact]
    public async Task RefreshPreview_Exposes_Bindable_Dry_Run_State()
    {
        var root = Path.Combine(Path.GetTempPath(), "raaya-workbench-bindable-preview");
        var localPath = Path.Combine(root, "dist", "app.js");
        var profile = new ServerProfile("stage", "Staging", "stage.example.test", 22, "deploy", "/srv/app", ServerAuthenticationMode.SshKey, "key:stage");
        var queue = new DeploymentQueueViewModel();
        var transport = new FakeTransport();
        var sut = CreateSut(queue, new ServersViewModel(new FakeStore(profile), transport), transport);

        await sut.LoadServersAsync(CancellationToken.None);
        queue.AddFile(localPath);

        sut.RefreshDryRunPreview(root);

        Assert.NotNull(sut.PreviewPlan);
        Assert.True(sut.PreviewPlan!.IsDryRun);
        var operation = Assert.Single(sut.PreviewPlan.Operations);
        Assert.Equal("/srv/app/dist/app.js", operation.RemotePath);
    }

    [Fact]
    public void Preview_Requires_A_Selected_Server()
    {
        var transport = new FakeTransport();
        var sut = CreateSut(
            new DeploymentQueueViewModel(),
            new ServersViewModel(new FakeStore(), transport),
            transport);

        Assert.Throws<InvalidOperationException>(() => sut.CreateDryRunPreview(Path.GetTempPath()));
    }

    private static DeploymentWorkspaceViewModel CreateSut(
        DeploymentQueueViewModel queue,
        ServersViewModel servers,
        IRemoteTransport transport)
    {
        var planner = new DeploymentPlanner();
        return new DeploymentWorkspaceViewModel(
            queue,
            servers,
            new DeploymentDryRunViewModel(planner),
            planner,
            new DeploymentExecutor(transport),
            new FakeHistoryStore());
    }

    private sealed class FakeStore(params ServerProfile[] profiles) : IServerProfileStore
    {
        private readonly List<ServerProfile> _profiles = [.. profiles];
        public Task<IReadOnlyList<ServerProfile>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ServerProfile>>(_profiles);
        public Task UpsertAsync(ServerProfile profile, CancellationToken cancellationToken) { _profiles.RemoveAll(x => x.Id == profile.Id); _profiles.Add(profile); return Task.CompletedTask; }
        public Task DeleteAsync(string id, CancellationToken cancellationToken) { _profiles.RemoveAll(x => x.Id == id); return Task.CompletedTask; }
    }

    private sealed class FakeHistoryStore : IDeploymentHistoryStore
    {
        private readonly List<DeploymentHistoryEntry> _entries = [];
        public Task<IReadOnlyList<DeploymentHistoryEntry>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DeploymentHistoryEntry>>(_entries);
        public Task AppendAsync(DeploymentHistoryEntry entry, CancellationToken cancellationToken) { _entries.Add(entry); return Task.CompletedTask; }
    }

    private sealed class FakeTransport : IRemoteTransport
    {
        public Task TestConnectionAsync(ServerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task UploadAsync(ServerProfile profile, string localPath, string remotePath, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
