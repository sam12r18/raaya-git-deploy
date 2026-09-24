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

    Task<IReadOnlyList<GitCommitInfo>> GetCommitsBetweenAsync(
        string repositoryPath,
        string baseRef,
        string targetRef,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GitChange>> GetCommitChangesAsync(
        string repositoryPath,
        string commitSha,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GitChange>>(Array.Empty<GitChange>());

    Task<IReadOnlyList<GitChange>> GetChangesBetweenAsync(
        string repositoryPath,
        string baseRef,
        string targetRef,
        CancellationToken cancellationToken);

    Task<string> GetCommitFileDiffAsync(
        string repositoryPath,
        string commitSha,
        string repositoryRelativePath,
        CancellationToken cancellationToken) =>
        Task.FromResult(string.Empty);

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
