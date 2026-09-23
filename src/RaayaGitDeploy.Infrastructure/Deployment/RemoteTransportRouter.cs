using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Infrastructure.Deployment;

/// <summary>
/// Keeps transport selection at the Desktop/agent infrastructure boundary.
/// Deployment planning and history remain transport-neutral.
/// </summary>
public sealed class RemoteTransportRouter : IRemoteTransport
{
    private readonly IReadOnlyDictionary<ServerTransportKind, IRemoteTransport> _transports;

    public RemoteTransportRouter(IEnumerable<KeyValuePair<ServerTransportKind, IRemoteTransport>> transports)
    {
        ArgumentNullException.ThrowIfNull(transports);
        _transports = transports.ToDictionary(static pair => pair.Key, static pair => pair.Value);
    }

    public Task TestConnectionAsync(ServerProfile profile, CancellationToken cancellationToken) =>
        Resolve(profile).TestConnectionAsync(profile, cancellationToken);

    public Task<IReadOnlyList<string>> ListAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) =>
        Resolve(profile).ListAsync(profile, remotePath, cancellationToken);

    public Task UploadAsync(ServerProfile profile, string localPath, string remotePath, CancellationToken cancellationToken) =>
        Resolve(profile).UploadAsync(profile, localPath, remotePath, cancellationToken);

    public Task DeleteAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) =>
        Resolve(profile).DeleteAsync(profile, remotePath, cancellationToken);

    private IRemoteTransport Resolve(ServerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (_transports.TryGetValue(profile.Transport, out var transport))
        {
            return transport;
        }

        throw new NotSupportedException($"Remote transport '{profile.Transport}' is not configured on this Desktop/agent host.");
    }
}
