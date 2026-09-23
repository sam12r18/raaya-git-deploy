using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Infrastructure.Deployment;

/// <summary>
/// Resolves Desktop/agent-only FTP credentials. Raw secrets never enter the shared deployment plan/history contracts.
/// </summary>
public interface IFtpCredentialResolver
{
    ValueTask<FtpCredential> ResolveAsync(ServerProfile profile, CancellationToken cancellationToken);
}

public sealed record FtpCredential(string Username, string Password);

/// <summary>
/// Adapter boundary for the concrete FTP/FTPS library. FTPS certificate validation must remain enabled by the adapter.
/// </summary>
public interface IFtpClientAdapter : IAsyncDisposable
{
    Task ConnectAsync(
        string host,
        int port,
        FtpCredential credential,
        bool useTls,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListAsync(string remotePath, CancellationToken cancellationToken);
    Task UploadAsync(string localPath, string remotePath, CancellationToken cancellationToken);
    Task DeleteAsync(string remotePath, CancellationToken cancellationToken);
}

/// <summary>
/// FTP/FTPS transport kept entirely at the Desktop/agent infrastructure boundary.
/// </summary>
public sealed class FtpRemoteTransport : IRemoteTransport
{
    private readonly Func<ServerProfile, IFtpClientAdapter> _clientFactory;
    private readonly IFtpCredentialResolver _credentialResolver;

    public FtpRemoteTransport(
        Func<ServerProfile, IFtpClientAdapter> clientFactory,
        IFtpCredentialResolver credentialResolver)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _credentialResolver = credentialResolver ?? throw new ArgumentNullException(nameof(credentialResolver));
    }

    public async Task TestConnectionAsync(ServerProfile profile, CancellationToken cancellationToken)
    {
        await using var client = await ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListAsync(
        ServerProfile profile,
        string remotePath,
        CancellationToken cancellationToken)
    {
        await using var client = await ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return await client.ListAsync(remotePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task UploadAsync(
        ServerProfile profile,
        string localPath,
        string remotePath,
        CancellationToken cancellationToken)
    {
        await using var client = await ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        await client.UploadAsync(localPath, remotePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(
        ServerProfile profile,
        string remotePath,
        CancellationToken cancellationToken)
    {
        await using var client = await ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        await client.DeleteAsync(remotePath, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IFtpClientAdapter> ConnectAsync(
        ServerProfile profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();

        if (profile.Transport is not (ServerTransportKind.Ftp or ServerTransportKind.Ftps))
        {
            throw new ArgumentException("FTP transport only accepts FTP or FTPS server profiles.", nameof(profile));
        }

        if (profile.AuthenticationMode != ServerAuthenticationMode.ExternalCredentialReference)
        {
            throw new InvalidOperationException("FTP/FTPS requires an external Desktop/agent credential reference.");
        }

        var credential = await _credentialResolver.ResolveAsync(profile, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(credential.Username) || string.IsNullOrEmpty(credential.Password))
        {
            throw new InvalidOperationException("The referenced FTP credential is incomplete.");
        }

        var client = _clientFactory(profile)
            ?? throw new InvalidOperationException("FTP client factory returned no client.");

        try
        {
            await client.ConnectAsync(
                profile.Host,
                profile.Port,
                credential,
                useTls: profile.Transport == ServerTransportKind.Ftps,
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
