using FluentFTP;
using System.Net;
using System.Net.Security;

namespace RaayaGitDeploy.Infrastructure.Deployment;

/// <summary>
/// FluentFTP-backed Desktop/agent adapter. FTPS is explicit TLS and certificate validation is fail-closed.
/// </summary>
public sealed class FluentFtpClientAdapter : IFtpClientAdapter
{
    private AsyncFtpClient? _client;

    public async Task ConnectAsync(
        string host,
        int port,
        FtpCredential credential,
        bool useTls,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_client is not null)
        {
            throw new InvalidOperationException("FTP client is already connected or initialized.");
        }

        var client = new AsyncFtpClient(host, credential.Username, credential.Password, port);
        client.Config.EncryptionMode = useTls ? FtpEncryptionMode.Explicit : FtpEncryptionMode.None;
        client.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;
        client.Config.ValidateAnyCertificate = false;

        if (useTls)
        {
            client.ValidateCertificate += (_, args) =>
            {
                args.Accept = args.PolicyErrors == SslPolicyErrors.None;
            };
        }

        try
        {
            await client.Connect(cancellationToken).ConfigureAwait(false);
            _client = client;
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> ListAsync(string remotePath, CancellationToken cancellationToken)
    {
        var client = GetConnectedClient();
        var items = await client.GetListing(remotePath, cancellationToken).ConfigureAwait(false);
        return items.Select(item => item.FullName).ToArray();
    }

    public async Task UploadAsync(string localPath, string remotePath, CancellationToken cancellationToken)
    {
        var client = GetConnectedClient();
        var status = await client.UploadFile(
            localPath,
            remotePath,
            FtpRemoteExists.Overwrite,
            createRemoteDir: true,
            token: cancellationToken).ConfigureAwait(false);

        if (status == FtpStatus.Failed)
        {
            throw new IOException($"FTP upload failed for remote path '{remotePath}'.");
        }
    }

    public Task DeleteAsync(string remotePath, CancellationToken cancellationToken)
    {
        return GetConnectedClient().DeleteFile(remotePath, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is null)
        {
            return;
        }

        var client = _client;
        _client = null;
        await client.DisposeAsync().ConfigureAwait(false);
    }

    private AsyncFtpClient GetConnectedClient()
    {
        return _client is { IsConnected: true }
            ? _client
            : throw new InvalidOperationException("FTP client is not connected.");
    }
}
