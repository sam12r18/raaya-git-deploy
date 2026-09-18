using System.Text.Json;
using RaayaGitDeploy.Core.Commands;

namespace RaayaGitDeploy.Infrastructure.Commands;

public sealed class JsonSavedCommandStore : ISavedCommandStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonSavedCommandStore(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
        StoragePath = Path.GetFullPath(storagePath);
    }

    public string StoragePath { get; }

    public async Task<IReadOnlyList<SavedCommand>> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpsertAsync(SavedCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        Validate(command);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var commands = (await LoadCoreAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var index = commands.FindIndex(candidate => string.Equals(candidate.Id, command.Id, StringComparison.Ordinal));
            if (index >= 0)
                commands[index] = command;
            else
                commands.Add(command);

            await SaveCoreAsync(commands, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var commands = (await LoadCoreAsync(cancellationToken).ConfigureAwait(false))
                .Where(command => !string.Equals(command.Id, id, StringComparison.Ordinal))
                .ToList();
            await SaveCoreAsync(commands, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<SavedCommand>> LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(StoragePath))
            return Array.Empty<SavedCommand>();

        try
        {
            await using var stream = File.OpenRead(StoragePath);
            var commands = await JsonSerializer.DeserializeAsync<List<SavedCommand>>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
            return commands is null ? Array.Empty<SavedCommand>() : commands;
        }
        catch (JsonException)
        {
            PreserveMalformedFile();
            return Array.Empty<SavedCommand>();
        }
    }

    private async Task SaveCoreAsync(IReadOnlyCollection<SavedCommand> commands, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(StoragePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var temporaryPath = StoragePath + ".tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            await JsonSerializer.SerializeAsync(stream, commands, SerializerOptions, cancellationToken).ConfigureAwait(false);

        File.Move(temporaryPath, StoragePath, overwrite: true);
    }

    private void PreserveMalformedFile()
    {
        var corruptPath = $"{StoragePath}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        File.Move(StoragePath, corruptPath);
    }

    private static void Validate(SavedCommand command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CommandText);
    }
}
