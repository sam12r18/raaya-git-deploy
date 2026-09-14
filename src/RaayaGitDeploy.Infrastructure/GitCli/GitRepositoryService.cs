using System.Text;
using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Core.Git.Parsing;

namespace RaayaGitDeploy.Infrastructure.GitCli;

public sealed class GitRepositoryService : IGitRepositoryService
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

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

    public Task<string> GetDiffAsync(
        string repositoryPath,
        string path,
        string? baseRef,
        CancellationToken cancellationToken) =>
        GetDiffAsync(
            repositoryPath,
            path,
            baseRef,
            long.MaxValue,
            cancellationToken);

    public async Task<string> GetDiffAsync(
        string repositoryPath,
        string path,
        string? baseRef,
        long maxUntrackedPreviewBytes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (baseRef is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(baseRef);
        }

        if (maxUntrackedPreviewBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxUntrackedPreviewBytes));
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

        if (baseRef is not null || !string.IsNullOrEmpty(result.StandardOutput))
        {
            return result.StandardOutput;
        }

        var workingTreeChanges = await GetWorkingTreeChangesAsync(root, cancellationToken);
        var isUntracked = workingTreeChanges.Any(change =>
            change.Kind == GitChangeKind.Untracked &&
            string.Equals(change.Path, path, StringComparison.Ordinal));

        if (!isUntracked)
        {
            return result.StandardOutput;
        }

        var fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            return result.StandardOutput;
        }

        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        if (bytes.Contains((byte)0))
        {
            return string.Empty;
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return string.Empty;
        }

        return BuildAllAddedPreview(path, text);
    }

    private static string BuildAllAddedPreview(string path, string text)
    {
        var normalizedText = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalizedText.Split('\n');
        var lineCount = lines.Length > 0 && lines[^1].Length == 0
            ? lines.Length - 1
            : lines.Length;

        var preview = new StringBuilder();
        preview.AppendLine("--- /dev/null");
        preview.AppendLine($"+++ b/{path}");
        preview.AppendLine($"@@ -0,0 +1,{lineCount} @@");

        for (var index = 0; index < lineCount; index++)
        {
            preview.Append('+').AppendLine(lines[index]);
        }

        return preview.ToString();
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
