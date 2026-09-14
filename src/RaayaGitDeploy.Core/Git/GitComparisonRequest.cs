namespace RaayaGitDeploy.Core.Git;

public sealed record GitComparisonRequest
{
    public GitComparisonRequest(string baseRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseRef);
        BaseRef = baseRef;
    }

    public string BaseRef { get; }
}
