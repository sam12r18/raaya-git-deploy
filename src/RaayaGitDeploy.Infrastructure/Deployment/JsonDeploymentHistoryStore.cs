using System.Text.Json;
using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Infrastructure.Deployment;

public sealed class JsonDeploymentHistoryStore : IDeploymentHistoryStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonDeploymentHistoryStore(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
        StoragePath = Path.GetFullPath(storagePath);
    }

    public string StoragePath { get; }

    public async Task<IReadOnlyList<DeploymentHistoryEntry>> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(StoragePath)) return Array.Empty<DeploymentHistoryEntry>();
            await using var stream = File.OpenRead(StoragePath);
            return await JsonSerializer.DeserializeAsync<List<DeploymentHistoryEntry>>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false)
                ?? Array.Empty<DeploymentHistoryEntry>();
        }
        finally { _gate.Release(); }
    }

    public async Task AppendAsync(DeploymentHistoryEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<DeploymentHistoryEntry> entries;
            if (File.Exists(StoragePath))
            {
                await using var input = File.OpenRead(StoragePath);
                entries = await JsonSerializer.DeserializeAsync<List<DeploymentHistoryEntry>>(input, SerializerOptions, cancellationToken).ConfigureAwait(false) ?? [];
            }
            else entries = [];

            entries.Insert(0, entry);
            var directory = Path.GetDirectoryName(StoragePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = StoragePath + ".tmp";
            await using (var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
                await JsonSerializer.SerializeAsync(output, entries, SerializerOptions, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, StoragePath, overwrite: true);
        }
        finally { _gate.Release(); }
    }
}
