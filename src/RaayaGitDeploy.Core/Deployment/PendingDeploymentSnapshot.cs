using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Core.Deployment;

public sealed record PendingDeploymentSnapshot(
    string RepositoryPath,
    string Branch,
    string? FromHead,
    string ToHead,
    IReadOnlyList<GitCommitInfo> Commits,
    IReadOnlyList<GitChange> Changes,
    bool RequiresBaseline);
