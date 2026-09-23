using RaayaGitDeploy.Core.Commands;

namespace RaayaGitDeploy.Presentation.Commands;

public sealed class CommandsViewModel
{
    private readonly ISavedCommandStore _store;
    private readonly ICommandExecutionService _executor;
    private string? _repositoryPath;

    public CommandsViewModel(ISavedCommandStore store, ICommandExecutionService executor)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public IReadOnlyList<SavedCommand> Commands { get; private set; } = [];
    public SavedCommand? SelectedCommand { get; set; }
    public bool IsRunning { get; private set; }
    public string? ExecutionStatus { get; private set; }

    public void SetRepository(string? repositoryPath)
    {
        _repositoryPath = string.IsNullOrWhiteSpace(repositoryPath) ? null : repositoryPath;
        SelectedCommand = null;
        ExecutionStatus = null;
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        Commands = await _store.LoadAsync(cancellationToken);
        SelectedCommand = null;
        ExecutionStatus = null;
    }

    public async Task SaveAsync(SavedCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await _store.UpsertAsync(command, cancellationToken);
        Commands = await _store.LoadAsync(cancellationToken);
        SelectedCommand = Commands.FirstOrDefault(item => item.Id == command.Id);
        ExecutionStatus = $"Saved '{command.Name}'.";
    }

    public async Task DeleteSelectedAsync(CancellationToken cancellationToken)
    {
        var selected = SelectedCommand ?? throw new InvalidOperationException("Select a saved command before deleting it.");
        await _store.DeleteAsync(selected.Id, cancellationToken);
        Commands = await _store.LoadAsync(cancellationToken);
        SelectedCommand = null;
        ExecutionStatus = $"Deleted '{selected.Name}'.";
    }

    public async Task RunSelectedAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
            throw new InvalidOperationException("Wait for the current saved command to finish before starting another one.");

        var selected = SelectedCommand ?? throw new InvalidOperationException("Select a saved command before running it.");
        var workingDirectory = string.IsNullOrWhiteSpace(selected.WorkingDirectoryOverride)
            ? _repositoryPath
            : selected.WorkingDirectoryOverride;

        if (string.IsNullOrWhiteSpace(workingDirectory))
            throw new InvalidOperationException("Open a repository or configure a working-directory override before running this command.");

        IsRunning = true;
        ExecutionStatus = $"Running '{selected.Name}'...";
        try
        {
            await _executor.ExecuteAsync(selected.CommandText, workingDirectory, cancellationToken);
            ExecutionStatus = $"Finished '{selected.Name}'. Review Terminal output for details.";
        }
        catch (OperationCanceledException)
        {
            ExecutionStatus = $"Cancelled '{selected.Name}'.";
            throw;
        }
        catch
        {
            ExecutionStatus = $"Failed '{selected.Name}'. Review Terminal output and the error details.";
            throw;
        }
        finally
        {
            IsRunning = false;
        }
    }
}
