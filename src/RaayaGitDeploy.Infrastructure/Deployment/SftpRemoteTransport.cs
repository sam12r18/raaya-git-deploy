using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Infrastructure.Deployment;

public interface IHostKeyVerifier
{
    bool Verify(ServerProfile profile, string fingerprint);
}

public interface ISftpClientAdapter : IAsyncDisposable
{
    Task ConnectAsync(Func<string, bool> verifyHostKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListAsync(string remotePath, CancellationToken cancellationToken);
    Task UploadAsync(string localPath, string remotePath, CancellationToken cancellationToken);
    Task DeleteAsync(string remotePath, CancellationToken cancellationToken);
}

public sealed class SftpRemoteTransport : IRemoteTransport
{
    private readonly Func<ServerProfile, ISftpClientAdapter> _clientFactory;
    private readonly IHostKeyVerifier _hostKeyVerifier;

    public SftpRemoteTransport(
        Func<ServerProfile, ISftpClientAdapter> clientFactory,
        IHostKeyVerifier hostKeyVerifier)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _hostKeyVerifier = hostKeyVerifier ?? throw new ArgumentNullException(nameof(hostKeyVerifier));
    }

    public async Task TestConnectionAsync(ServerProfile profile, CancellationToken cancellationToken)
    {
        await using var client = await ConnectVerifiedAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListAsync(
        ServerProfile profile,
        string remotePath,
        CancellationToken cancellationToken)
    {
        await using var client = await ConnectVerifiedAsync(profile, cancellationToken).ConfigureAwait(false);
        return await client.ListAsync(remotePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task UploadAsync(
        ServerProfile profile,
        string localPath,
        string remotePath,
        CancellationToken cancellationToken)
    {
        await using var client = await ConnectVerifiedAsync(profile, cancellationToken).ConfigureAwait(false);
        await client.UploadAsync(localPath, remotePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(
        ServerProfile profile,
        string remotePath,
        CancellationToken cancellationToken)
    {
        await using var client = await ConnectVerifiedAsync(profile, cancellationToken).ConfigureAwait(false);
        await client.DeleteAsync(remotePath, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ISftpClientAdapter> ConnectVerifiedAsync(
        ServerProfile profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();

        var client = _clientFactory(profile)
            ?? throw new InvalidOperationException("SFTP client factory returned no client.");

        try
        {
            await client.ConnectAsync(
                fingerprint => _hostKeyVerifier.Verify(profile, fingerprint),
                cancellationToken).ConfigureAwait(false);
            return client;
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
