using System.Text.Json;
using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Infrastructure.Deployment;

public sealed class JsonServerProfileStore : IServerProfileStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonServerProfileStore(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
        StoragePath = Path.GetFullPath(storagePath);
    }

    public string StoragePath { get; }

    public async Task<IReadOnlyList<ServerProfile>> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await LoadCoreAsync(cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async Task UpsertAsync(ServerProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Validate(profile);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var profiles = (await LoadCoreAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var index = profiles.FindIndex(candidate => string.Equals(candidate.Id, profile.Id, StringComparison.Ordinal));
            if (index >= 0) profiles[index] = profile; else profiles.Add(profile);
            await SaveCoreAsync(profiles, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var profiles = (await LoadCoreAsync(cancellationToken).ConfigureAwait(false))
                .Where(profile => !string.Equals(profile.Id, id, StringComparison.Ordinal)).ToList();
            await SaveCoreAsync(profiles, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task<IReadOnlyList<ServerProfile>> LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(StoragePath)) return Array.Empty<ServerProfile>();
        await using var stream = File.OpenRead(StoragePath);
        var profiles = await JsonSerializer.DeserializeAsync<List<ServerProfile>>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        return profiles ?? Array.Empty<ServerProfile>();
    }

    private async Task SaveCoreAsync(IReadOnlyCollection<ServerProfile> profiles, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(StoragePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporaryPath = StoragePath + ".tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            await JsonSerializer.SerializeAsync(stream, profiles, SerializerOptions, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, StoragePath, overwrite: true);
    }

    private static void Validate(ServerProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Host);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Username);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.RemoteRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.KeyReference);
        if (profile.Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(profile), "Server port must be between 1 and 65535.");
    }
}
