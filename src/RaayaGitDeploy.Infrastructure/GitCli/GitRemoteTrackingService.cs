using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Infrastructure.GitCli;

public sealed class GitRemoteTrackingService : IGitRemoteTrackingService
{
    private readonly IGitProcessRunner _runner;

    public GitRemoteTrackingService(IGitProcessRunner runner) =>
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));

    public async Task FetchAsync(string repositoryPath, string remoteName, CancellationToken cancellationToken)
    {
        Validate(repositoryPath, remoteName, nameof(remoteName));
        var root = await GetRootAsync(repositoryPath, cancellationToken);
        var arguments = new[] { "fetch", "--prune", "--", remoteName };
        var result = await _runner.RunAsync(root, arguments, cancellationToken);
        EnsureSuccess(arguments, result);
    }

    public async Task<string> GetRemoteRevisionAsync(string repositoryPath, string remoteName, string branchName, CancellationToken cancellationToken)
    {
        Validate(repositoryPath, remoteName, nameof(remoteName));
        Validate(repositoryPath, branchName, nameof(branchName));
        var root = await GetRootAsync(repositoryPath, cancellationToken);
        var remoteRef = $"refs/remotes/{remoteName}/{branchName}";
        var arguments = new[] { "rev-parse", "--verify", "--end-of-options", $"{remoteRef}^{{commit}}" };
        var result = await _runner.RunAsync(root, arguments, cancellationToken);
        EnsureSuccess(arguments, result);
        var revision = result.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidOperationException("Remote revision lookup returned no commit.");
        return revision;
    }

    private async Task<string> GetRootAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var args = new[] { "rev-parse", "--show-toplevel" };
        var result = await _runner.RunAsync(path, args, cancellationToken);
        EnsureSuccess(args, result);
        return result.StandardOutput.Trim();
    }

    private static void Validate(string repositoryPath, string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.StartsWith('-', StringComparison.Ordinal))
            throw new ArgumentException("Git names must not be parsed as command options.", parameterName);
    }

    private static void EnsureSuccess(IReadOnlyList<string> arguments, GitCommandResult result)
    {
        if (result.ExitCode == 0) return;
        var error = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput.Trim() : result.StandardError.Trim();
        throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed with exit code {result.ExitCode}: {error}");
    }
}
