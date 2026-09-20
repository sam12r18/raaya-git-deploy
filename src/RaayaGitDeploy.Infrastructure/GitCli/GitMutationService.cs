using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Infrastructure.GitCli;

public sealed class GitMutationService : IGitMutationService
{
    private readonly IGitProcessRunner _runner;

    public GitMutationService(IGitProcessRunner runner) =>
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));

    public async Task StageAsync(string repositoryPath, IReadOnlyList<string> repositoryRelativePaths, CancellationToken cancellationToken)
    {
        var (root, paths) = await PreparePathsAsync(repositoryPath, repositoryRelativePaths, cancellationToken);
        await RunRequiredAsync(root, ["add", "--", .. paths], cancellationToken);
    }

    public async Task UnstageAsync(string repositoryPath, IReadOnlyList<string> repositoryRelativePaths, CancellationToken cancellationToken)
    {
        var (root, paths) = await PreparePathsAsync(repositoryPath, repositoryRelativePaths, cancellationToken);
        await RunRequiredAsync(root, ["restore", "--staged", "--", .. paths], cancellationToken);
    }

    public async Task<string> CommitStagedAsync(string repositoryPath, string message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var normalizedMessage = message.Trim();
        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        await RunRequiredAsync(root, ["commit", "-m", normalizedMessage], cancellationToken);
        return await RunRequiredAsync(root, ["rev-parse", "HEAD"], cancellationToken);
    }

    private async Task<(string Root, string[] Paths)> PreparePathsAsync(string repositoryPath, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0) throw new ArgumentException("Select at least one repository path.", nameof(paths));

        var root = await ResolveRootAsync(repositoryPath, cancellationToken);
        var normalized = paths.Select(path => ValidateAndNormalizePath(root, path)).Distinct(StringComparer.Ordinal).ToArray();
        return (root, normalized);
    }

    private async Task<string> ResolveRootAsync(string repositoryPath, CancellationToken cancellationToken) =>
        await RunRequiredAsync(repositoryPath, ["rev-parse", "--show-toplevel"], cancellationToken);

    private static string ValidateAndNormalizePath(string root, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = path.Replace('\\', '/');
        if (Path.IsPathRooted(normalized)) throw new ArgumentException("Git mutation path must be repository-relative.", nameof(path));
        var rootFullPath = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine(rootFullPath, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(rootFullPath, candidate);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            throw new ArgumentException("Git mutation path must remain inside the repository root.", nameof(path));
        return normalized;
    }

    private async Task<string> RunRequiredAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(workingDirectory, arguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            var error = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput.Trim() : result.StandardError.Trim();
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed with exit code {result.ExitCode}: {error}");
        }
        return result.StandardOutput.Trim();
    }
}
