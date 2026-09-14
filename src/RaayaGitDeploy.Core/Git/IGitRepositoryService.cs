namespace RaayaGitDeploy.Core.Git;

public interface IGitRepositoryService
{
    Task<GitRepositoryContext> GetContextAsync(
        string path,
        CancellationToken cancellationToken);
}
