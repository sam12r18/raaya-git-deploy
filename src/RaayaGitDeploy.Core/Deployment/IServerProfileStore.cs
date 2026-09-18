namespace RaayaGitDeploy.Core.Deployment;

public interface IServerProfileStore
{
    Task<IReadOnlyList<ServerProfile>> LoadAsync(CancellationToken cancellationToken);
    Task UpsertAsync(ServerProfile profile, CancellationToken cancellationToken);
    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
