namespace RaayaGitDeploy.Core.Deployment;

public interface IRemoteTransport
{
    Task TestConnectionAsync(ServerProfile profile, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken);
    Task UploadAsync(ServerProfile profile, string localPath, string remotePath, CancellationToken cancellationToken);
    Task DeleteAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken);
}
