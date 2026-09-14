namespace RaayaGitDeploy.Core.Git;

public interface IGitRepositoryService
{
    Task<GitRepositoryContext> GetContextAsync(
        string path,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(
        string path,
        CancellationToken cancellationToken);
}
