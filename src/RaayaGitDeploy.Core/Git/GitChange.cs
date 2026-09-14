namespace RaayaGitDeploy.Core.Git;

public sealed record GitChange(
    string Path,
    GitChangeKind Kind,
    string? OriginalPath = null);
