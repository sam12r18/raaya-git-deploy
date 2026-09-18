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

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        Profiles = await _store.LoadAsync(cancellationToken);
        SelectedProfile = Profiles.FirstOrDefault();
    }

    public async Task SaveAsync(ServerProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await _store.UpsertAsync(profile, cancellationToken);
        Profiles = await _store.LoadAsync(cancellationToken);
        SelectedProfile = Profiles.FirstOrDefault(item => item.Id == profile.Id);
    }

    public async Task DeleteSelectedAsync(CancellationToken cancellationToken)
    {
        var selected = SelectedProfile
            ?? throw new InvalidOperationException("Select a server profile before deleting it.");

        await _store.DeleteAsync(selected.Id, cancellationToken);
        Profiles = await _store.LoadAsync(cancellationToken);
        SelectedProfile = null;
    }

    public Task TestSelectedConnectionAsync(CancellationToken cancellationToken)
    {
        var selected = SelectedProfile
            ?? throw new InvalidOperationException("Select a server profile before testing the connection.");

        return _transport.TestConnectionAsync(selected, cancellationToken);
    }
}
