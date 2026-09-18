namespace RaayaGitDeploy.Core.Commands;

public interface ISavedCommandStore
{
    string StoragePath { get; }

    Task<IReadOnlyList<SavedCommand>> LoadAsync(CancellationToken cancellationToken);
    Task UpsertAsync(SavedCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
