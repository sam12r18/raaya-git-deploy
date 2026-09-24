namespace RaayaGitDeploy.Core.Git;

public sealed record GitUpdateResult(
    string RepositoryPath,
    string Branch,
    string OldHead,
    string NewHead,
    bool Changed);

public interface IGitUpdateService
{
    Task<GitUpdateResult> UpdateFastForwardOnlyAsync(
        string repositoryPath,
        CancellationToken cancellationToken);
}
