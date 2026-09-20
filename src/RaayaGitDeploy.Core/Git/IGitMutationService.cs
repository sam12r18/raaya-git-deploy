namespace RaayaGitDeploy.Core.Git;

public interface IGitMutationService
{
    Task StageAsync(
        string repositoryPath,
        IReadOnlyList<string> repositoryRelativePaths,
        CancellationToken cancellationToken);

    Task UnstageAsync(
        string repositoryPath,
        IReadOnlyList<string> repositoryRelativePaths,
        CancellationToken cancellationToken);

    Task<string> CommitStagedAsync(
        string repositoryPath,
        string message,
        CancellationToken cancellationToken);
}
