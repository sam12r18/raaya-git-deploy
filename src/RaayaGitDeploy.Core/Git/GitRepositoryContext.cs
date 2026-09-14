namespace RaayaGitDeploy.Core.Git;

public sealed record GitRepositoryContext(
    string RootPath,
    string BranchName,
    string HeadSha);
