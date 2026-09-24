using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitRepositoryExactRangeTests
{
    [Fact]
    public async Task Exact_range_returns_only_committed_range_and_preserves_rename()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), $"rgd-range-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var runner = new GitProcessRunner();
            await RunRequiredAsync(runner, root, ["init", "-b", "main"], cancellationToken);
            await RunRequiredAsync(runner, root, ["config", "user.email", "test@example.invalid"], cancellationToken);
            await RunRequiredAsync(runner, root, ["config", "user.name", "Raaya Range Test"], cancellationToken);

            await File.WriteAllTextAsync(Path.Combine(root, "old.php"), "<?php echo 'stable';\n", cancellationToken);
            await RunRequiredAsync(runner, root, ["add", "old.php"], cancellationToken);
            await RunRequiredAsync(runner, root, ["commit", "-m", "baseline"], cancellationToken);
            var baseSha = await ReadHeadAsync(runner, root, cancellationToken);

            await File.WriteAllTextAsync(Path.Combine(root, "added.txt"), "second\n", cancellationToken);
            await RunRequiredAsync(runner, root, ["add", "added.txt"], cancellationToken);
            await RunRequiredAsync(runner, root, ["commit", "-m", "add deploy file"], cancellationToken);
            var middleSha = await ReadHeadAsync(runner, root, cancellationToken);

            await RunRequiredAsync(runner, root, ["mv", "old.php", "new.php"], cancellationToken);
            await RunRequiredAsync(runner, root, ["commit", "-m", "rename deploy file"], cancellationToken);
            var targetSha = await ReadHeadAsync(runner, root, cancellationToken);

            // Working-tree-only state must never leak into a deployment commit range.
            await File.WriteAllTextAsync(Path.Combine(root, "pending-local.txt"), "not committed\n", cancellationToken);

            var service = new GitRepositoryService(runner);
            var commits = await service.GetCommitsBetweenAsync(root, baseSha, targetSha, cancellationToken);
            var changes = await service.GetChangesBetweenAsync(root, baseSha, targetSha, cancellationToken);

            Assert.Equal([targetSha, middleSha], commits.Select(commit => commit.Sha));
            Assert.Contains(commits, commit => commit.Subject == "rename deploy file");
            Assert.Contains(commits, commit => commit.Subject == "add deploy file");

            Assert.Contains(changes, change =>
                change.Kind == GitChangeKind.Added && change.Path == "added.txt");
            Assert.Contains(changes, change =>
                change.Kind == GitChangeKind.Renamed &&
                change.Path == "new.php" &&
                change.OriginalPath == "old.php");
            Assert.DoesNotContain(changes, change => change.Path == "pending-local.txt");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static async Task<string> ReadHeadAsync(
        GitProcessRunner runner,
        string root,
        CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(root, ["rev-parse", "HEAD"], cancellationToken);
        Assert.True(result.ExitCode == 0, result.StandardError);
        return result.StandardOutput.Trim();
    }

    private static async Task RunRequiredAsync(
        GitProcessRunner runner,
        string root,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(root, arguments, cancellationToken);
        Assert.True(result.ExitCode == 0, result.StandardError);
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories)
                     .OrderByDescending(static directory => directory.Length))
            File.SetAttributes(directory, FileAttributes.Normal);
        File.SetAttributes(path, FileAttributes.Normal);
        Directory.Delete(path, recursive: true);
    }
}
