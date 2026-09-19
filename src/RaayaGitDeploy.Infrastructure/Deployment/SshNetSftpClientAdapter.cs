using Renci.SshNet;
using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Infrastructure.Deployment;

public sealed class SshNetSftpClientAdapter : ISftpClientAdapter
{
    private readonly SftpClient _client;

    public SshNetSftpClientAdapter(ServerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.KeyReference))
            throw new InvalidOperationException("An SSH private-key path is required.");
        if (!File.Exists(profile.KeyReference))
            throw new FileNotFoundException("The configured SSH private key was not found.", profile.KeyReference);

        var key = new PrivateKeyFile(profile.KeyReference);
        var authentication = new PrivateKeyAuthenticationMethod(profile.Username, key);
        var connection = new ConnectionInfo(profile.Host, profile.Port, profile.Username, authentication);
        _client = new SftpClient(connection);
    }

    public async Task ConnectAsync(Func<string, bool> verifyHostKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifyHostKey);
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
        var entries = await Task.Run(
            () => _client.ListDirectory(remotePath).Select(item => item.FullName).ToArray(),
            cancellationToken).ConfigureAwait(false);
        return entries;
    }

    public async Task UploadAsync(string localPath, string remotePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(localPath);
        await Task.Run(() => _client.UploadFile(stream, remotePath, true), cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(string remotePath, CancellationToken cancellationToken) =>
        Task.Run(() => _client.DeleteFile(remotePath), cancellationToken);

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
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
