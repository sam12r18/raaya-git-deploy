namespace RaayaGitDeploy.Core.Git;

public sealed record GitWorkingTreeChange(
    string Path,
    GitChangeKind Kind,
    string? OriginalPath,
    bool IsStaged,
    bool IsUnstaged);
