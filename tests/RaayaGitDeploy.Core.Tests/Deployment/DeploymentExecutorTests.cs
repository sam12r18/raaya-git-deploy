using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Core.Tests.Deployment;

public sealed class DeploymentExecutorTests
{
    private static readonly ServerProfile Profile = new(
        "prod", "Production", "example.test", 22, "deploy", "/var/www/app",
        ServerAuthenticationMode.SshKey, "test-key");

    [Fact]
    public async Task ExecuteAsync_UploadsBeforeDeletes_AndRecordsPerItemResults()
    {
        var transport = new RecordingTransport();
        var executor = new DeploymentExecutor(transport);
        var plan = new DeploymentPlan(
        [
            new(DeploymentOperationKind.Delete, string.Empty, "/var/www/app/old.txt"),
            new(DeploymentOperationKind.Upload, "C:/repo/app.txt", "/var/www/app/app.txt"),
            new(DeploymentOperationKind.Skip, "C:/repo/skip.txt", "/var/www/app/skip.txt")
        ], false);

        var result = await executor.ExecuteAsync(Profile, plan, CancellationToken.None);

        Assert.Equal(["upload:/var/www/app/app.txt", "delete:/var/www/app/old.txt"], transport.Calls);
        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items, item => Assert.True(item.Succeeded));
    }

    [Fact]
    public async Task ExecuteAsync_UploadFailure_BlocksAllDeletes()
    {
        var transport = new RecordingTransport { FailUploadRemotePath = "/var/www/app/bad.txt" };
        var executor = new DeploymentExecutor(transport);
        var plan = new DeploymentPlan(
        [
            new(DeploymentOperationKind.Upload, "C:/repo/good.txt", "/var/www/app/good.txt"),
            new(DeploymentOperationKind.Upload, "C:/repo/bad.txt", "/var/www/app/bad.txt"),
            new(DeploymentOperationKind.Delete, string.Empty, "/var/www/app/old.txt")
        ], false);

        var result = await executor.ExecuteAsync(Profile, plan, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.DoesNotContain(transport.Calls, call => call.StartsWith("delete:", StringComparison.Ordinal));
        Assert.Contains(result.Items, item => item.Operation.RemotePath.EndsWith("bad.txt", StringComparison.Ordinal) && !item.Succeeded);
        Assert.Contains(result.Items, item => item.Operation.Kind == DeploymentOperationKind.Delete && item.Status == DeploymentItemStatus.Blocked);
    }

    [Fact]
    public async Task ExecuteAsync_DryRunPlan_IsRejectedWithoutRemoteMutation()
    {
        var transport = new RecordingTransport();
        var executor = new DeploymentExecutor(transport);
        var plan = new DeploymentPlan(
            [new(DeploymentOperationKind.Upload, "C:/repo/app.txt", "/var/www/app/app.txt")], true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(Profile, plan, CancellationToken.None));

        Assert.Empty(transport.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_StopsBeforeFollowingOperations()
    {
        using var cts = new CancellationTokenSource();
        var transport = new RecordingTransport { CancelAfterFirstUpload = cts };
        var executor = new DeploymentExecutor(transport);
        var plan = new DeploymentPlan(
        [
            new(DeploymentOperationKind.Upload, "C:/repo/one.txt", "/var/www/app/one.txt"),
            new(DeploymentOperationKind.Upload, "C:/repo/two.txt", "/var/www/app/two.txt"),
            new(DeploymentOperationKind.Delete, string.Empty, "/var/www/app/old.txt")
        ], false);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(Profile, plan, cts.Token));

        Assert.Equal(["upload:/var/www/app/one.txt"], transport.Calls);
    }

    private sealed class RecordingTransport : IRemoteTransport
    {
        public List<string> Calls { get; } = [];
        public string? FailUploadRemotePath { get; init; }
        public CancellationTokenSource? CancelAfterFirstUpload { get; init; }

        public Task TestConnectionAsync(ServerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task UploadAsync(ServerProfile profile, string localPath, string remotePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add($"upload:{remotePath}");
            if (string.Equals(remotePath, FailUploadRemotePath, StringComparison.Ordinal))
                throw new IOException("Simulated upload failure.");
            CancelAfterFirstUpload?.Cancel();
            return Task.CompletedTask;
        }

        public Task DeleteAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add($"delete:{remotePath}");
            return Task.CompletedTask;
        }
    }
}
