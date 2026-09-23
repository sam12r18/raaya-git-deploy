using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Infrastructure.Deployment;

namespace RaayaGitDeploy.Infrastructure.Tests;

public sealed class FtpRemoteTransportTests
{
    [Fact]
    public async Task TestConnection_UsesPlainFtpForFtpProfile()
    {
        var adapter = new RecordingFtpClientAdapter();
        var transport = CreateTransport(adapter);
        var profile = CreateProfile(ServerTransportKind.Ftp);

        await transport.TestConnectionAsync(profile, CancellationToken.None);

        Assert.True(adapter.Connected);
        Assert.False(adapter.UseTls);
        Assert.Equal("example.test", adapter.Host);
        Assert.Equal(21, adapter.Port);
        Assert.True(adapter.Disposed);
    }

    [Fact]
    public async Task TestConnection_EnablesTlsForFtpsProfile()
    {
        var adapter = new RecordingFtpClientAdapter();
        var transport = CreateTransport(adapter);
        var profile = CreateProfile(ServerTransportKind.Ftps);

        await transport.TestConnectionAsync(profile, CancellationToken.None);

        Assert.True(adapter.Connected);
        Assert.True(adapter.UseTls);
        Assert.Equal("deploy-user", adapter.Credential?.Username);
        Assert.Equal("secret", adapter.Credential?.Password);
    }

    [Fact]
    public async Task Upload_ConnectsAndDelegatesRemotePath()
    {
        var adapter = new RecordingFtpClientAdapter();
        var transport = CreateTransport(adapter);

        await transport.UploadAsync(
            CreateProfile(ServerTransportKind.Ftps),
            "local.zip",
            "/public_html/app.zip",
            CancellationToken.None);

        Assert.Equal(("local.zip", "/public_html/app.zip"), adapter.Upload);
        Assert.True(adapter.Disposed);
    }

    [Fact]
    public async Task TestConnection_RejectsSftpProfileBeforeResolvingCredentials()
    {
        var resolver = new RecordingCredentialResolver();
        var transport = new FtpRemoteTransport(_ => new RecordingFtpClientAdapter(), resolver);
        var profile = new ServerProfile(
            "server-1", "SFTP", "example.test", 22, "/", "key-ref",
            ServerTransportKind.Sftp, ServerAuthenticationMode.SshKey);

        await Assert.ThrowsAsync<ArgumentException>(
            () => transport.TestConnectionAsync(profile, CancellationToken.None));

        Assert.False(resolver.Called);
    }

    [Fact]
    public async Task TestConnection_DisposesClientWhenConnectFails()
    {
        var adapter = new RecordingFtpClientAdapter { ConnectException = new InvalidOperationException("connect failed") };
        var transport = CreateTransport(adapter);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => transport.TestConnectionAsync(CreateProfile(ServerTransportKind.Ftps), CancellationToken.None));

        Assert.True(adapter.Disposed);
    }

    private static FtpRemoteTransport CreateTransport(RecordingFtpClientAdapter adapter) =>
        new(_ => adapter, new RecordingCredentialResolver());

    private static ServerProfile CreateProfile(ServerTransportKind transport) =>
        new(
            "server-1",
            transport == ServerTransportKind.Ftps ? "cPanel FTPS" : "cPanel FTP",
            "example.test",
            21,
            "/public_html",
            "credential-ref",
            transport,
            ServerAuthenticationMode.ExternalCredentialReference);

    private sealed class RecordingCredentialResolver : IFtpCredentialResolver
    {
        public bool Called { get; private set; }

        public ValueTask<FtpCredential> ResolveAsync(ServerProfile profile, CancellationToken cancellationToken)
        {
            Called = true;
            return ValueTask.FromResult(new FtpCredential("deploy-user", "secret"));
        }
    }

    private sealed class RecordingFtpClientAdapter : IFtpClientAdapter
    {
        public bool Connected { get; private set; }
        public bool UseTls { get; private set; }
        public bool Disposed { get; private set; }
        public string? Host { get; private set; }
        public int Port { get; private set; }
        public FtpCredential? Credential { get; private set; }
        public (string Local, string Remote)? Upload { get; private set; }
        public Exception? ConnectException { get; init; }

        public Task ConnectAsync(string host, int port, FtpCredential credential, bool useTls, CancellationToken cancellationToken)
        {
            if (ConnectException is not null)
            {
                throw ConnectException;
            }

            Connected = true;
            Host = host;
            Port = port;
            Credential = credential;
            UseTls = useTls;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListAsync(string remotePath, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task UploadAsync(string localPath, string remotePath, CancellationToken cancellationToken)
        {
            Upload = (localPath, remotePath);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string remotePath, CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
