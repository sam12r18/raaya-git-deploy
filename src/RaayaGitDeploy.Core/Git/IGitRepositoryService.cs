namespace RaayaGitDeploy.Core.Git;

public interface IGitRepositoryService
{
    Task<GitRepositoryContext> GetContextAsync(
        string path,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(
        string path,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GitCommitInfo>> GetRecentCommitsAsync(
        string repositoryPath,
        int limit,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GitCommitInfo>>(Array.Empty<GitCommitInfo>());

    Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(
        string repositoryPath,
        GitComparisonRequest request,
        CancellationToken cancellationToken);

    Task<string> GetDiffAsync(
        string repositoryPath,
        string path,
        string? baseRef,
        CancellationToken cancellationToken);

    Task<string> GetDiffAsync(
        string repositoryPath,
        string path,
        string? baseRef,
        long maxUntrackedPreviewBytes,
        CancellationToken cancellationToken);
}
