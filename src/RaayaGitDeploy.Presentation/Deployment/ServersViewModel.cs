using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Presentation.Deployment;

public sealed class ServersViewModel
{
    private readonly IServerProfileStore _store;
    private readonly IRemoteTransport _transport;

    public ServersViewModel(IServerProfileStore store, IRemoteTransport transport)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public IReadOnlyList<ServerProfile> Profiles { get; private set; } = [];
    public ServerProfile? SelectedProfile { get; set; }
    public bool IsBusy { get; private set; }
    public string? StatusMessage { get; private set; }
    public bool HasError { get; private set; }
    public bool CanTestConnection => !IsBusy && SelectedProfile is not null;

    public Task LoadAsync(CancellationToken cancellationToken) =>
        RunAsync("Loading server profiles...", async () =>
        {
            Profiles = await _store.LoadAsync(cancellationToken);
            SelectedProfile = Profiles.FirstOrDefault();
            StatusMessage = Profiles.Count == 0
                ? "No server profiles yet. Add a profile before creating a deployment queue."
                : $"Loaded {Profiles.Count} server profile(s).";
        });

    public Task SaveAsync(ServerProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return RunAsync("Saving server profile...", async () =>
        {
            await _store.UpsertAsync(profile, cancellationToken);
            Profiles = await _store.LoadAsync(cancellationToken);
            SelectedProfile = Profiles.FirstOrDefault(item => item.Id == profile.Id);
            StatusMessage = $"Saved server profile '{profile.DisplayName}'.";
        });
    }

    public Task DeleteSelectedAsync(CancellationToken cancellationToken)
    {
        var selected = SelectedProfile
            ?? throw new InvalidOperationException("Select a server profile before deleting it.");

        return RunAsync("Deleting server profile...", async () =>
        {
            await _store.DeleteAsync(selected.Id, cancellationToken);
            Profiles = await _store.LoadAsync(cancellationToken);
            SelectedProfile = Profiles.FirstOrDefault();
            StatusMessage = Profiles.Count == 0
                ? "No server profiles yet."
                : $"Deleted '{selected.DisplayName}'.";
        });
    }

    public Task TestSelectedConnectionAsync(CancellationToken cancellationToken)
    {
        var selected = SelectedProfile
            ?? throw new InvalidOperationException("Select a server profile before testing the connection.");

        return RunAsync($"Testing connection to {selected.DisplayName}...", async () =>
        {
            await _transport.TestConnectionAsync(selected, cancellationToken);
            StatusMessage = $"Connection to '{selected.DisplayName}' succeeded.";
        });
    }

    private async Task RunAsync(string pendingMessage, Func<Task> operation)
    {
        if (IsBusy)
            throw new InvalidOperationException("Wait for the current server operation to finish.");

        IsBusy = true;
        HasError = false;
        StatusMessage = pendingMessage;
        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Server operation cancelled.";
            throw;
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = ex.Message;
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
