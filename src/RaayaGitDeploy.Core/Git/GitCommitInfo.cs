namespace RaayaGitDeploy.Core.Git;

public sealed record GitCommitInfo(
    string Sha,
    string ShortSha,
    string Subject,
    string AuthorName,
    DateTimeOffset AuthorDate);
