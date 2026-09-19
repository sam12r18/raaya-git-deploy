using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Infrastructure.Deployment;

namespace RaayaGitDeploy.Infrastructure.Tests;

public sealed class SftpRemoteTransportTests
{
    [Fact]
    public async Task TestConnection_requires_host_key_verification_before_connect_succeeds()
    {
        var client = new FakeSftpClient { PresentedHostKey = "SHA256:server-key" };
        var transport = new SftpRemoteTransport(
            _ => client,
            new FixedHostKeyVerifier("SHA256:trusted-key"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.TestConnectionAsync(Profile(), CancellationToken.None));

        Assert.Contains("host key", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(client.ConnectionAccepted);
    }

    [Fact]
    public async Task TestConnection_accepts_matching_host_key_and_disconnects()
    {
        var client = new FakeSftpClient { PresentedHostKey = "SHA256:trusted-key" };
        var transport = new SftpRemoteTransport(
            _ => client,
            new FixedHostKeyVerifier("SHA256:trusted-key"));

        await transport.TestConnectionAsync(Profile(), CancellationToken.None);

        Assert.True(client.ConnectionAccepted);
        Assert.True(client.Disposed);
    }

    [Fact]
    public async Task Upload_and_delete_delegate_only_after_verified_connection()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var client = new FakeSftpClient { PresentedHostKey = "SHA256:trusted-key" };
            var transport = new SftpRemoteTransport(
                _ => client,
                new FixedHostKeyVerifier("SHA256:trusted-key"));

            await transport.UploadAsync(Profile(), tempFile, "/srv/app/file.txt", CancellationToken.None);
            await transport.DeleteAsync(Profile(), "/srv/app/old.txt", CancellationToken.None);

            Assert.Equal((tempFile, "/srv/app/file.txt"), client.Uploaded);
            Assert.Equal("/srv/app/old.txt", client.DeletedPath);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static ServerProfile Profile() => new(
        "prod", "Production", "example.test", 22, "deploy", "/srv/app",
        ServerAuthenticationMode.SshKey, "keys/prod");

    private sealed class FixedHostKeyVerifier(string trustedFingerprint) : IHostKeyVerifier
    {
        public bool Verify(ServerProfile profile, string fingerprint) =>
            string.Equals(fingerprint, trustedFingerprint, StringComparison.Ordinal);
    }

    private sealed class FakeSftpClient : ISftpClientAdapter
    {
        public string PresentedHostKey { get; init; } = string.Empty;
        public bool ConnectionAccepted { get; private set; }
        public bool Disposed { get; private set; }
        public (string Local, string Remote)? Uploaded { get; private set; }
        public string? DeletedPath { get; private set; }

        public Task ConnectAsync(Func<string, bool> verifyHostKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConnectionAccepted = verifyHostKey(PresentedHostKey);
            if (!ConnectionAccepted)
            {
                throw new InvalidOperationException("Host key verification failed.");
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListAsync(string remotePath, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task UploadAsync(string localPath, string remotePath, CancellationToken cancellationToken)
        {
            Uploaded = (localPath, remotePath);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string remotePath, CancellationToken cancellationToken)
        {
            DeletedPath = remotePath;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
