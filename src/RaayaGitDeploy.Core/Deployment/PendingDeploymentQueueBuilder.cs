using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Core.Deployment;

public sealed record PendingDeploymentQueueBuildResult(
    IReadOnlyList<DeploymentQueueItem> Items,
    IReadOnlyList<string> Warnings);

public sealed class PendingDeploymentQueueBuilder
{
    public PendingDeploymentQueueBuildResult Build(
        string repositoryRoot,
        IReadOnlyList<GitChange> changes,
        DeploymentRuleSet rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(rules.GeneratedPaths);

        var root = Path.GetFullPath(repositoryRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var items = new Dictionary<string, DeploymentQueueItem>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        foreach (var change in changes)
        {
            ArgumentNullException.ThrowIfNull(change);

            switch (change.Kind)
            {
                case GitChangeKind.Added:
                case GitChangeKind.Modified:
                case GitChangeKind.Untracked:
                    AddItem(items, new DeploymentQueueItem(
                        ResolveRepositoryPath(root, change.Path),
                        DeploymentQueueSource.GitDetected));
                    break;

                case GitChangeKind.Deleted:
                    AddItem(items, new DeploymentQueueItem(
                        ResolveRepositoryPath(root, change.Path),
                        DeploymentQueueSource.GitDetected,
                        Action: DeploymentQueueAction.Delete));
                    break;

                case GitChangeKind.Renamed:
                    if (string.IsNullOrWhiteSpace(change.OriginalPath))
                        throw new InvalidOperationException($"Renamed Git path '{change.Path}' has no original path.");

                    AddItem(items, new DeploymentQueueItem(
                        ResolveRepositoryPath(root, change.Path),
                        DeploymentQueueSource.GitDetected));
                    AddItem(items, new DeploymentQueueItem(
                        ResolveRepositoryPath(root, change.OriginalPath),
                        DeploymentQueueSource.GitDetected,
                        Action: DeploymentQueueAction.Delete));
                    break;

                case GitChangeKind.Conflicted:
                    throw new InvalidOperationException(
                        $"Conflicted Git path cannot be prepared for deployment: {change.Path}");

                default:
                    throw new ArgumentOutOfRangeException(nameof(change.Kind), change.Kind, "Unsupported Git change kind.");
            }
        }

        foreach (var generatedPath in rules.GeneratedPaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(generatedPath);
            var fullPath = ResolveRepositoryPath(root, generatedPath);

            if (File.Exists(fullPath))
            {
                AddItem(items, new DeploymentQueueItem(fullPath, DeploymentQueueSource.GeneratedRule));
                continue;
            }

            if (Directory.Exists(fullPath))
            {
                foreach (var file in Directory
                             .EnumerateFiles(fullPath, "*", SearchOption.AllDirectories)
                             .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
                {
                    AddItem(items, new DeploymentQueueItem(
                        Path.GetFullPath(file),
                        DeploymentQueueSource.GeneratedRule));
                }

                continue;
            }

            warnings.Add($"Generated deployment path was not found: {generatedPath}");
        }

        return new PendingDeploymentQueueBuildResult(
            items.Values
                .OrderBy(static item => item.Action)
                .ThenBy(static item => item.LocalPath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            warnings);
    }

    private static void AddItem(
        IDictionary<string, DeploymentQueueItem> items,
        DeploymentQueueItem candidate)
    {
        var key = $"{candidate.Action}:{candidate.LocalPath}";
        if (!items.TryGetValue(key, out var existing))
        {
            items[key] = candidate;
            return;
        }

        if (candidate.Action == DeploymentQueueAction.Upload &&
            candidate.Source == DeploymentQueueSource.GeneratedRule &&
            existing.Source == DeploymentQueueSource.GitDetected)
        {
            items[key] = candidate;
        }
    }

    private static string ResolveRepositoryPath(string repositoryRoot, string repositoryRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRelativePath);
        if (Path.IsPathRooted(repositoryRelativePath))
            throw new InvalidOperationException($"Deployment path must be repository-relative: {repositoryRelativePath}");

        var localPath = repositoryRelativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(repositoryRoot, localPath));
        var relative = Path.GetRelativePath(repositoryRoot, fullPath);

        if (IsOutsideRepository(relative))
            throw new InvalidOperationException($"Deployment path must remain inside the repository root: {repositoryRelativePath}");

        return fullPath;
    }

    private static bool IsOutsideRepository(string relativePath) =>
        Path.IsPathRooted(relativePath) ||
        relativePath.Equals("..", StringComparison.Ordinal) ||
        relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
}
