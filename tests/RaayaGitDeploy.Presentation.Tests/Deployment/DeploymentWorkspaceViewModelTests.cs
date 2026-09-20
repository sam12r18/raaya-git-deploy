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

    [Fact]
    public async Task Execute_Rejects_Queue_Changes_After_Dry_Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "raaya-workbench-stale-queue");
        var profile = new ServerProfile("prod", "Production", "example.test", 22, "deploy", "/var/www/app", ServerAuthenticationMode.SshKey, "key:prod");
        var queue = new DeploymentQueueViewModel();
        var transport = new FakeTransport();
        var sut = CreateSut(queue, new ServersViewModel(new FakeStore(profile), transport), transport);

        await sut.LoadServersAsync(CancellationToken.None);
        queue.AddFile(Path.Combine(root, "first.txt"));
        sut.RefreshDryRunPreview(root);
        queue.AddFile(Path.Combine(root, "second.txt"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteAsync(root, CancellationToken.None));

        Assert.Contains("Run Dry Run again", error.Message, StringComparison.Ordinal);
        Assert.Empty(transport.Uploads);
    }

    [Fact]
    public async Task Execute_Rejects_Server_Changes_After_Dry_Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "raaya-workbench-stale-server");
        var production = new ServerProfile("prod", "Production", "prod.example.test", 22, "deploy", "/var/www/app", ServerAuthenticationMode.SshKey, "key:prod");
        var staging = new ServerProfile("stage", "Staging", "stage.example.test", 22, "deploy", "/var/www/app", ServerAuthenticationMode.SshKey, "key:stage");
        var queue = new DeploymentQueueViewModel();
        var transport = new FakeTransport();
        var servers = new ServersViewModel(new FakeStore(production, staging), transport);
        var sut = CreateSut(queue, servers, transport);

        await sut.LoadServersAsync(CancellationToken.None);
        servers.SelectedProfile = production;
        queue.AddFile(Path.Combine(root, "app.txt"));
        sut.RefreshDryRunPreview(root);
        servers.SelectedProfile = staging;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteAsync(root, CancellationToken.None));

        Assert.Contains("Run Dry Run again", error.Message, StringComparison.Ordinal);
        Assert.Empty(transport.Uploads);
    }

    [Fact]
    public async Task PrepareRetry_Requeues_Failed_Uploads_And_Restores_Server_Without_Reusing_Dry_Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "raaya-workbench-retry");
        var failedPath = Path.Combine(root, "dist", "app.js");
        var successfulPath = Path.Combine(root, "dist", "style.css");
        var profile = new ServerProfile("prod", "Production", "example.test", 22, "deploy", "/var/www/app", ServerAuthenticationMode.SshKey, "key:prod");
        var queue = new DeploymentQueueViewModel();
        var transport = new FakeTransport();
        var servers = new ServersViewModel(new FakeStore(profile), transport);
        var sut = CreateSut(queue, servers, transport);

        await sut.LoadServersAsync(CancellationToken.None);
        queue.AddFile(successfulPath);
        sut.RefreshDryRunPreview(root);
        var entry = new DeploymentHistoryEntry(
            "failed-run",
            DateTimeOffset.UtcNow,
            profile.Id,
            profile.DisplayName,
            false,
            [
                new(new DeploymentOperation(DeploymentOperationKind.Upload, failedPath, "/var/www/app/dist/app.js"), DeploymentItemStatus.Failed, "network error"),
                new(new DeploymentOperation(DeploymentOperationKind.Upload, successfulPath, "/var/www/app/dist/style.css"), DeploymentItemStatus.Succeeded)
            ]);

        sut.PrepareRetry(entry);

        var queued = Assert.Single(sut.Queue.Items);
        Assert.Equal(failedPath.Replace('\\', '/'), queued.LocalPath);
        Assert.Equal(profile, sut.Servers.SelectedProfile);
        Assert.Null(sut.PreviewPlan);
    }

    [Fact]
    public async Task PrepareRetry_Rejects_Missing_Original_Server_Profile()
    {
        var transport = new FakeTransport();
        var sut = CreateSut(new DeploymentQueueViewModel(), new ServersViewModel(new FakeStore(), transport), transport);
        await sut.LoadServersAsync(CancellationToken.None);
        var entry = new DeploymentHistoryEntry(
            "failed-run",
            DateTimeOffset.UtcNow,
            "deleted-profile",
            "Deleted server",
            false,
            [new(new DeploymentOperation(DeploymentOperationKind.Upload, Path.Combine(Path.GetTempPath(), "app.js"), "/app.js"), DeploymentItemStatus.Failed, "network error")]);

        var error = Assert.Throws<InvalidOperationException>(() => sut.PrepareRetry(entry));

        Assert.Contains("no longer available", error.Message, StringComparison.Ordinal);
        Assert.Empty(sut.Queue.Items);
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
        public List<(string LocalPath, string RemotePath)> Uploads { get; } = [];
        public Task TestConnectionAsync(ServerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task UploadAsync(ServerProfile profile, string localPath, string remotePath, CancellationToken cancellationToken) { Uploads.Add((localPath, remotePath)); return Task.CompletedTask; }
        public Task DeleteAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
