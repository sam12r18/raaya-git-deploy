namespace RaayaGitDeploy.Core.Deployment;

public sealed class DeploymentPlanner
{
    public DeploymentPlan Plan(
        string repositoryRoot,
        string remoteRoot,
        IEnumerable<string> localPaths,
        bool dryRun = false)
    {
        ArgumentNullException.ThrowIfNull(localPaths);

        return Plan(
            repositoryRoot,
            remoteRoot,
            localPaths.Select(path => new DeploymentQueueItem(
                path,
                DeploymentQueueSource.GitSelection)),
            dryRun);
    }

    public DeploymentPlan Plan(
        string repositoryRoot,
        string remoteRoot,
        IEnumerable<DeploymentQueueItem> items,
        bool dryRun = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteRoot);
        ArgumentNullException.ThrowIfNull(items);

        var normalizedRepositoryRoot = Path.GetFullPath(repositoryRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRemoteRoot = NormalizeRemoteRoot(remoteRoot);
        var operations = new List<DeploymentOperation>();

        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentException.ThrowIfNullOrWhiteSpace(item.LocalPath);

            var normalizedLocalPath = Path.IsPathRooted(item.LocalPath)
                ? Path.GetFullPath(item.LocalPath)
                : Path.GetFullPath(Path.Combine(normalizedRepositoryRoot, item.LocalPath));
            var relativePath = Path.GetRelativePath(normalizedRepositoryRoot, normalizedLocalPath);

            if (IsOutsideRepository(relativePath))
            {
                throw new InvalidOperationException(
                    $"Deployment path must remain inside the repository root: {item.LocalPath}");
            }

            var remotePath = string.IsNullOrWhiteSpace(item.RemotePath)
                ? MapRemotePath(normalizedRemoteRoot, relativePath)
                : ValidateExplicitRemotePath(normalizedRemoteRoot, item.RemotePath);

            var kind = item.Action switch
            {
                DeploymentQueueAction.Upload => DeploymentOperationKind.Upload,
                DeploymentQueueAction.Delete => DeploymentOperationKind.Delete,
                _ => throw new ArgumentOutOfRangeException(nameof(item.Action), item.Action, "Unsupported deployment queue action.")
            };

            operations.Add(new DeploymentOperation(kind, normalizedLocalPath, remotePath));
        }

        return new DeploymentPlan(operations, dryRun);
    }

    private static string MapRemotePath(string normalizedRemoteRoot, string relativePath)
    {
        var remoteRelativePath = relativePath.Replace('\\', '/');
        return normalizedRemoteRoot.Length == 0
            ? $"/{remoteRelativePath}"
            : $"{normalizedRemoteRoot}/{remoteRelativePath}";
    }

    private static string ValidateExplicitRemotePath(string normalizedRemoteRoot, string remotePath)
    {
        var normalized = NormalizeRemoteRoot(remotePath);
        if (normalizedRemoteRoot.Length == 0)
            return normalized.Length == 0 ? "/" : normalized;

        if (!string.Equals(normalized, normalizedRemoteRoot, StringComparison.Ordinal) &&
            !normalized.StartsWith($"{normalizedRemoteRoot}/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Remote deployment path must remain inside the configured remote root: {remotePath}");
        }

        return normalized;
    }

    private static bool IsOutsideRepository(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            return true;

        return relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string NormalizeRemoteRoot(string remoteRoot)
    {
        var normalized = remoteRoot.Replace('\\', '/');
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
            throw new ArgumentException("Remote root must be an absolute POSIX path.", nameof(remoteRoot));

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".."))
            throw new ArgumentException("Remote root must not contain traversal segments.", nameof(remoteRoot));

        return segments.Length == 0 ? string.Empty : $"/{string.Join('/', segments)}";
    }
}
