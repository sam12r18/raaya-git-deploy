namespace RaayaGitDeploy.Core.Deployment;

public sealed class DeploymentPlanner
{
    public DeploymentPlan Plan(
        string repositoryRoot,
        string remoteRoot,
        IEnumerable<string> localPaths,
        bool dryRun = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteRoot);
        ArgumentNullException.ThrowIfNull(localPaths);

        var normalizedRepositoryRoot = Path.GetFullPath(repositoryRoot);
        var normalizedRemoteRoot = NormalizeRemoteRoot(remoteRoot);
        var operations = new List<DeploymentOperation>();

        foreach (var localPath in localPaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(localPath);

            var normalizedLocalPath = Path.GetFullPath(localPath);
            var relativePath = Path.GetRelativePath(normalizedRepositoryRoot, normalizedLocalPath);

            if (IsOutsideRepository(relativePath))
            {
                throw new InvalidOperationException($"Deployment path must remain inside the repository root: {localPath}");
            }

            var remoteRelativePath = relativePath.Replace('\\', '/');
            var remotePath = normalizedRemoteRoot.Length == 0
                ? $"/{remoteRelativePath}"
                : $"{normalizedRemoteRoot}/{remoteRelativePath}";

            operations.Add(new DeploymentOperation(
                DeploymentOperationKind.Upload,
                normalizedLocalPath,
                remotePath));
        }

        return new DeploymentPlan(operations, dryRun);
    }

    public DeploymentPlan Plan(
        DeploymentProject project,
        IEnumerable<string> localPaths,
        bool dryRun = false)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(localPaths);

        var candidatePaths = localPaths.ToArray();
        var basePlan = Plan(project.RepositoryRoot, project.ApplicationRoot, candidatePaths, dryRun);
        var operations = new List<DeploymentOperation>(basePlan.Operations.Count);

        foreach (var operation in basePlan.Operations)
        {
            var relativePath = Path.GetRelativePath(project.RepositoryRoot, operation.LocalPath)
                .Replace('\\', '/');

            if (project.IsProtected(relativePath))
            {
                operations.Add(operation with { Kind = DeploymentOperationKind.Skip });
                continue;
            }

            operations.Add(operation);
        }

        return new DeploymentPlan(operations, dryRun);
    }

    private static bool IsOutsideRepository(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return true;
        }

        return relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string NormalizeRemoteRoot(string remoteRoot)
    {
        var normalized = remoteRoot.Replace('\\', '/');
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Remote root must be an absolute POSIX path.", nameof(remoteRoot));
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Remote root must not contain traversal segments.", nameof(remoteRoot));
        }

        return segments.Length == 0 ? string.Empty : $"/{string.Join('/', segments)}";
    }
}
