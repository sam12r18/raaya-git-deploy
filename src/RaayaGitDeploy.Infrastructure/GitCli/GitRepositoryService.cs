using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Core.Git.Parsing;

namespace RaayaGitDeploy.Infrastructure.GitCli;

public sealed class GitRepositoryService : IGitRepositoryService
{
    private readonly IGitProcessRunner _runner;

    public GitRepositoryService(IGitProcessRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public async Task<GitRepositoryContext> GetContextAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var root = await RunRequiredAsync(
            path,
            new[] { "rev-parse", "--show-toplevel" },
            cancellationToken);

        var headSha = await RunRequiredAsync(
            root,
            new[] { "rev-parse", "HEAD" },
            cancellationToken);

        var branchResult = await _runner.RunAsync(
            root,
            new[] { "branch", "--show-current" },
            cancellationToken);

        if (branchResult.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildGitFailureMessage(
                new[] { "branch", "--show-current" },
                branchResult));
        }

        var branchName = branchResult.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(branchName))
        {
            branchName = $"detached@{headSha[..Math.Min(8, headSha.Length)]}";
        }

        return new GitRepositoryContext(
            Path.GetFullPath(root),
            branchName,
            headSha);
    }

    public async Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var root = await RunRequiredAsync(
            path,
            new[] { "rev-parse", "--show-toplevel" },
            cancellationToken);

        var arguments = new[]
        {
            "-c",
            "status.renames=true",
            "status",
            "--porcelain=v2",
            "-z",
            "--untracked-files=all"
        };

        var result = await _runner.RunAsync(root, arguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildGitFailureMessage(arguments, result));
        }

        return GitPorcelainV2Parser.Parse(result.StandardOutput);
    }

    public async Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(
        string repositoryPath,
        GitComparisonRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(request);

        var root = await RunRequiredAsync(
            repositoryPath,
            new[] { "rev-parse", "--show-toplevel" },
            cancellationToken);

        var arguments = new[]
        {
            "diff",
            "--name-status",
            "-M",
            "-z",
            $"{request.BaseRef}...HEAD"
        };

        var result = await _runner.RunAsync(root, arguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildGitFailureMessage(arguments, result));
        }

        return GitNameStatusParser.Parse(result.StandardOutput);
    }

    public async Task<string> GetDiffAsync(
        string repositoryPath,
        string path,
        string? baseRef,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (baseRef is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(baseRef);
        }

        var root = await RunRequiredAsync(
            repositoryPath,
            new[] { "rev-parse", "--show-toplevel" },
            cancellationToken);

        string[] arguments = baseRef is null
            ? ["diff", "--no-color", "HEAD", "--", path]
            : ["diff", "--no-color", $"{baseRef}...HEAD", "--", path];

        var result = await _runner.RunAsync(root, arguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildGitFailureMessage(arguments, result));
        }

        return result.StandardOutput;
    }

    private async Task<string> RunRequiredAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
            workingDirectory,
            arguments,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildGitFailureMessage(arguments, result));
        }

        var output = result.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} returned no output.");
        }

        return output;
    }

    private static string BuildGitFailureMessage(
        IReadOnlyList<string> arguments,
        GitCommandResult result)
    {
        var error = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

        return $"git {string.Join(' ', arguments)} failed with exit code {result.ExitCode}: {error}";
    }
}
