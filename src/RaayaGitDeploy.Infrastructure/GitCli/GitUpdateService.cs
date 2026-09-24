using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Infrastructure.GitCli;

public sealed class GitUpdateService(IGitProcessRunner runner) : IGitUpdateService
{
    public async Task<GitUpdateResult> UpdateFastForwardOnlyAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        var statusArguments = new[] { "status", "--porcelain=v2", "-z", "--untracked-files=all" };
        var status = await runner.RunAsync(repositoryPath, statusArguments, cancellationToken).ConfigureAwait(false);
        EnsureSucceeded(statusArguments, status);
        if (!string.IsNullOrEmpty(status.StandardOutput))
        {
            throw new InvalidOperationException(
                "Automatic update requires a clean working tree. Commit, stash, or discard local changes before pulling.");
        }

        var branchArguments = new[] { "branch", "--show-current" };
        var branchResult = await runner.RunAsync(repositoryPath, branchArguments, cancellationToken).ConfigureAwait(false);
        EnsureSucceeded(branchArguments, branchResult);
        var branch = branchResult.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(branch))
        {
            throw new InvalidOperationException(
                "Automatic update is unavailable while HEAD is detached. Check out a branch before pulling.");
        }

        var upstreamArguments = new[] { "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}" };
        var upstreamResult = await runner.RunAsync(repositoryPath, upstreamArguments, cancellationToken).ConfigureAwait(false);
        if (upstreamResult.ExitCode != 0 || string.IsNullOrWhiteSpace(upstreamResult.StandardOutput))
        {
            var detail = GetError(upstreamResult);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(detail)
                    ? "Automatic update requires a configured upstream branch."
                    : $"Automatic update requires a configured upstream branch: {detail}");
        }

        var oldHead = await RunRequiredOutputAsync(
            repositoryPath,
            ["rev-parse", "HEAD"],
            cancellationToken).ConfigureAwait(false);

        var pullArguments = new[] { "pull", "--ff-only" };
        var pullResult = await runner.RunAsync(repositoryPath, pullArguments, cancellationToken).ConfigureAwait(false);
        EnsureSucceeded(pullArguments, pullResult);

        var newHead = await RunRequiredOutputAsync(
            repositoryPath,
            ["rev-parse", "HEAD"],
            cancellationToken).ConfigureAwait(false);

        return new GitUpdateResult(
            repositoryPath,
            branch,
            oldHead,
            newHead,
            !string.Equals(oldHead, newHead, StringComparison.Ordinal));
    }

    private async Task<string> RunRequiredOutputAsync(
        string repositoryPath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(repositoryPath, arguments, cancellationToken).ConfigureAwait(false);
        EnsureSucceeded(arguments, result);

        var output = result.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} returned no output.");

        return output;
    }

    private static void EnsureSucceeded(IReadOnlyList<string> arguments, GitCommandResult result)
    {
        if (result.ExitCode == 0)
            return;

        throw new InvalidOperationException(
            $"git {string.Join(' ', arguments)} failed with exit code {result.ExitCode}: {GetError(result)}");
    }

    private static string GetError(GitCommandResult result) =>
        string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();
}
