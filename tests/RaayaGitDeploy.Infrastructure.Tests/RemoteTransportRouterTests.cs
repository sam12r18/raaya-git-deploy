using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Infrastructure.Deployment;

namespace RaayaGitDeploy.Infrastructure.Tests;

public sealed class RemoteTransportRouterTests
{
    [Fact]
    public async Task TestConnection_routes_to_profile_transport_only()
    {
        var sftp = new RecordingTransport();
        var ftp = new RecordingTransport();
        var router = new RemoteTransportRouter(
        [
            new(ServerTransportKind.Sftp, sftp),
            new(ServerTransportKind.Ftp, ftp)
        ]);
        var profile = Profile(ServerTransportKind.Ftp);

        await router.TestConnectionAsync(profile, CancellationToken.None);

        Assert.Equal(0, sftp.TestConnectionCalls);
        Assert.Equal(1, ftp.TestConnectionCalls);
    }

    [Fact]
    public async Task Upload_routes_without_leaking_transport_details_into_operation_contract()
    {
        var ftps = new RecordingTransport();
        var router = new RemoteTransportRouter(
        [
            new(ServerTransportKind.Ftps, ftps)
        ]);
        var profile = Profile(ServerTransportKind.Ftps);

        await router.UploadAsync(profile, "local.zip", "/public_html/local.zip", CancellationToken.None);

        Assert.Equal(("local.zip", "/public_html/local.zip"), ftps.LastUpload);
    }

    [Fact]
    public async Task Missing_transport_fails_before_any_remote_operation()
    {
        var router = new RemoteTransportRouter(Array.Empty<KeyValuePair<ServerTransportKind, IRemoteTransport>>());

        var error = await Assert.ThrowsAsync<NotSupportedException>(
            () => router.TestConnectionAsync(Profile(ServerTransportKind.Ftp), CancellationToken.None));

        Assert.Contains("Ftp", error.Message, StringComparison.Ordinal);
    }

    private static ServerProfile Profile(ServerTransportKind transport) => new(
        "server-1",
        "cPanel",
        "example.test",
        transport == ServerTransportKind.Sftp ? 22 : 21,
        "deploy",
        "/public_html",
        transport == ServerTransportKind.Sftp
            ? ServerAuthenticationMode.SshKey
            : ServerAuthenticationMode.ExternalCredentialReference,
        transport == ServerTransportKind.Sftp ? "ssh-key" : "credential-ref",
        transport);

    private sealed class RecordingTransport : IRemoteTransport
    {
        public int TestConnectionCalls { get; private set; }
        public (string LocalPath, string RemotePath)? LastUpload { get; private set; }

        public Task TestConnectionAsync(ServerProfile profile, CancellationToken cancellationToken)
        {
            TestConnectionCalls++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task UploadAsync(ServerProfile profile, string localPath, string remotePath, CancellationToken cancellationToken)
        {
            LastUpload = (localPath, remotePath);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
