namespace RaayaGitDeploy.Core.Deployment;

public enum DeploymentStrategy
{
    FileSync,
    Laravel
}

public sealed record DeploymentProject(
    string Id,
    string DisplayName,
    string RepositoryRoot,
    string RemoteName,
    string Branch,
    string ServerProfileId,
    string ApplicationRoot,
    string? PublicRoot,
    DeploymentStrategy Strategy,
    IReadOnlyList<string> ProtectedPaths)
{
    public static DeploymentProject Create(
        string id,
        string displayName,
        string repositoryRoot,
        string remoteName,
        string branch,
        string serverProfileId,
        string applicationRoot,
        string? publicRoot = null,
        DeploymentStrategy strategy = DeploymentStrategy.FileSync,
        IEnumerable<string>? protectedPaths = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationRoot);

        if (!Path.IsPathFullyQualified(repositoryRoot))
        {
            throw new ArgumentException("Repository root must be an absolute local path.", nameof(repositoryRoot));
        }

        ValidateRemoteRoot(applicationRoot, nameof(applicationRoot));
        if (!string.IsNullOrWhiteSpace(publicRoot))
        {
            ValidateRemoteRoot(publicRoot, nameof(publicRoot));
        }

        var protectedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".env",
            "storage",
            "uploads"
        };

        if (protectedPaths is not null)
        {
            foreach (var path in protectedPaths)
            {
                var normalized = NormalizeProtectedPath(path);
                protectedSet.Add(normalized);
            }
        }

        return new DeploymentProject(
            id.Trim(),
            displayName.Trim(),
            Path.GetFullPath(repositoryRoot),
            remoteName.Trim(),
            branch.Trim(),
            serverProfileId.Trim(),
            NormalizeRemoteRoot(applicationRoot),
            string.IsNullOrWhiteSpace(publicRoot) ? null : NormalizeRemoteRoot(publicRoot),
            strategy,
            protectedSet.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public bool IsProtected(string repositoryRelativePath)
    {
        var candidate = NormalizeProtectedPath(repositoryRelativePath);
        return ProtectedPaths.Any(protectedPath =>
            candidate.Equals(protectedPath, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith($"{protectedPath}/", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeProtectedPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = path.Replace('\\', '/').Trim('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Protected path must be repository-relative and must not contain traversal segments.", nameof(path));
        }

        return string.Join('/', segments);
    }

    private static void ValidateRemoteRoot(string path, string parameterName)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Remote root must be an absolute POSIX path.", parameterName);
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Remote root must not contain traversal segments.", parameterName);
        }
    }

    private static string NormalizeRemoteRoot(string path)
    {
        var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 ? "/" : $"/{string.Join('/', segments)}";
    }
}
