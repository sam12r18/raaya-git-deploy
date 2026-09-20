using System.Text;
using Renci.SshNet;
using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Infrastructure.Deployment;

public sealed class SshNetSftpClientAdapter : ISftpClientAdapter
{
    private readonly ServerProfile _profile;
    private readonly ISecretStore _secretStore;
    private SftpClient? _client;
    private PrivateKeyFile? _privateKey;
    private MemoryStream? _privateKeyStream;

    public SshNetSftpClientAdapter(ServerProfile profile, ISecretStore secretStore)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    }

    public async Task ConnectAsync(Func<string, bool> verifyHostKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifyHostKey);
        cancellationToken.ThrowIfCancellationRequested();

        if (!SecretReference.TryParse(_profile.KeyReference, out var secretReference))
            throw new InvalidOperationException(
                "The SSH credential must be an opaque secret:// reference. Re-save this host profile to migrate legacy private-key paths.");

        var privateKeyText = await _secretStore.GetAsync(secretReference, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(privateKeyText))
            throw new InvalidOperationException("The SSH private key referenced by this host profile was not found in the protected secret store.");

        var privateKeyBytes = Encoding.UTF8.GetBytes(privateKeyText);
        try
        {
            _privateKeyStream = new MemoryStream(privateKeyBytes, writable: false);
            _privateKey = new PrivateKeyFile(_privateKeyStream);
            var authentication = new PrivateKeyAuthenticationMethod(_profile.Username, _privateKey);
            var connection = new ConnectionInfo(_profile.Host, _profile.Port, _profile.Username, authentication);
            _client = new SftpClient(connection);
        }
        finally
        {
            Array.Clear(privateKeyBytes, 0, privateKeyBytes.Length);
        }

        var accepted = false;
        _client.HostKeyReceived += (_, args) =>
        {
            var fingerprint = Convert.ToHexString(args.FingerPrint);
            accepted = verifyHostKey(fingerprint);
            args.CanTrust = accepted;
        };

        await Task.Run(_client.Connect, cancellationToken).ConfigureAwait(false);
        if (!accepted)
            throw new InvalidOperationException("The SSH host key was not accepted.");
    }

    public async Task<IReadOnlyList<string>> ListAsync(string remotePath, CancellationToken cancellationToken)
    {
        var client = RequireClient();
        var entries = await Task.Run(
            () => client.ListDirectory(remotePath).Select(item => item.FullName).ToArray(),
            cancellationToken).ConfigureAwait(false);
        return entries;
    }

    public async Task UploadAsync(string localPath, string remotePath, CancellationToken cancellationToken)
    {
        var client = RequireClient();
        await using var stream = File.OpenRead(localPath);
        await Task.Run(() => client.UploadFile(stream, remotePath, true), cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(string remotePath, CancellationToken cancellationToken)
    {
        var client = RequireClient();
        return Task.Run(() => client.DeleteFile(remotePath), cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _privateKey?.Dispose();
        _privateKeyStream?.Dispose();
        return ValueTask.CompletedTask;
    }

    private SftpClient RequireClient() =>
        _client ?? throw new InvalidOperationException("The SFTP client is not connected.");
}

public sealed class TofuHostKeyVerifier : IHostKeyVerifier
{
    private readonly string _storePath;
    private readonly object _gate = new();

    public TofuHostKeyVerifier(string storePath)
    {
        _storePath = string.IsNullOrWhiteSpace(storePath)
            ? throw new ArgumentException("A host-key store path is required.", nameof(storePath))
            : storePath;
    }

    public bool Verify(ServerProfile profile, string fingerprint)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(fingerprint)) return false;

        lock (_gate)
        {
            var entries = Load();
            var key = $"{profile.Host.Trim().ToLowerInvariant()}:{profile.Port}";
            if (entries.TryGetValue(key, out var pinned))
                return string.Equals(pinned, fingerprint, StringComparison.OrdinalIgnoreCase);

            entries[key] = fingerprint;
            var directory = Path.GetDirectoryName(_storePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllLines(_storePath, entries.OrderBy(item => item.Key).Select(item => $"{item.Key} {item.Value}"));
            return true;
        }
    }

    private Dictionary<string, string> Load()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(_storePath)) return result;

        foreach (var line in File.ReadLines(_storePath))
        {
            var split = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (split.Length == 2) result[split[0]] = split[1];
        }
        return result;
    }
}
