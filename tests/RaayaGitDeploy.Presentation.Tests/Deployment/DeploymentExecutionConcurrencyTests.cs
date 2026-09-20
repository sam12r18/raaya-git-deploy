using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.Presentation.Tests.Deployment;

public sealed class DeploymentExecutionConcurrencyTests
{
    [Fact]
    public async Task Execute_Rejects_Concurrent_Deployment_Without_Second_Remote_Mutation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"raaya-workbench-concurrent-deploy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var localPath = Path.Combine(root, "app.txt");
        await File.WriteAllTextAsync(localPath, "concurrency-test");

        try
        {
            var profile = new ServerProfile("prod", "Production", "example.test", 22, "deploy", "/var/www/app", ServerAuthenticationMode.SshKey, "key:prod");
            var queue = new DeploymentQueueViewModel();
            var transport = new BlockingTransport();
            var servers = new ServersViewModel(new FakeStore(profile), transport);
            var planner = new DeploymentPlanner();
            var sut = new DeploymentWorkspaceViewModel(queue, servers, new DeploymentDryRunViewModel(planner), planner, new DeploymentExecutor(transport), new FakeHistoryStore());

            await sut.LoadServersAsync(CancellationToken.None);
            queue.AddFile(localPath);
            sut.RefreshDryRunPreview(root);

            var first = sut.ExecuteAsync(root, CancellationToken.None);
            var uploadStarted = await Task.WhenAny(transport.UploadStarted.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(transport.UploadStarted.Task, uploadStarted);
            Assert.True(sut.IsExecuting);

            InvalidOperationException? concurrentError = null;
            try
            {
                await sut.ExecuteAsync(root, CancellationToken.None);
            }
            catch (InvalidOperationException exception)
            {
                concurrentError = exception;
            }

            Assert.NotNull(concurrentError);
            Assert.Contains("already in progress", concurrentError.Message);
            Assert.Equal(1, transport.UploadCount);

            transport.AllowUpload.TrySetResult(true);
            await first;

            Assert.False(sut.IsExecuting);
            Assert.Equal(1, transport.UploadCount);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private sealed class FakeStore(ServerProfile profile) : IServerProfileStore
    {
        public Task<IReadOnlyList<ServerProfile>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ServerProfile>>([profile]);
        public Task UpsertAsync(ServerProfile value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeHistoryStore : IDeploymentHistoryStore
    {
        private readonly List<DeploymentHistoryEntry> _entries = [];
        public Task<IReadOnlyList<DeploymentHistoryEntry>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DeploymentHistoryEntry>>(_entries);
        public Task AppendAsync(DeploymentHistoryEntry entry, CancellationToken cancellationToken)
        {
            _entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingTransport : IRemoteTransport
    {
        private int _uploadCount;
        public TaskCompletionSource<bool> UploadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> AllowUpload { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int UploadCount => Volatile.Read(ref _uploadCount);

        public Task TestConnectionAsync(ServerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);

        public async Task UploadAsync(ServerProfile profile, string localPath, string remotePath, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _uploadCount);
            UploadStarted.TrySetResult(true);
            await AllowUpload.Task.WaitAsync(cancellationToken);
        }

        public Task DeleteAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
