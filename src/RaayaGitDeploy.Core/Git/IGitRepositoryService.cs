namespace RaayaGitDeploy.Core.Git;

public interface IGitRepositoryService
{
    Task<GitRepositoryContext> GetContextAsync(
        string path,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(
        string path,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(
        string repositoryPath,
        GitComparisonRequest request,
        CancellationToken cancellationToken);

    Task<string> GetDiffAsync(
        string repositoryPath,
        string path,
        string? baseRef,
        CancellationToken cancellationToken);
}
