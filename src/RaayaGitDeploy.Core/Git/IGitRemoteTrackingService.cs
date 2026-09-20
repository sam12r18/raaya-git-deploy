namespace RaayaGitDeploy.Core.Git;

public interface IGitRemoteTrackingService
{
    Task FetchAsync(string repositoryPath, string remoteName, CancellationToken cancellationToken);

    Task<string> GetRemoteRevisionAsync(
        string repositoryPath,
        string remoteName,
        string branchName,
        CancellationToken cancellationToken);
}
