using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitMutationServiceTests
{
    [Fact]
    public async Task StageUnstageAndCommitStagedAsync_MutatesRealRepositoryExplicitly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), $"rgd-mutation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var runner = new GitProcessRunner();
            await RunAsync(runner, root, ["init", "-b", "main"], cancellationToken);
            await RunAsync(runner, root, ["config", "user.email", "test@example.invalid"], cancellationToken);
            await RunAsync(runner, root, ["config", "user.name", "Raaya Test"], cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "app.txt"), "initial\n", cancellationToken);
            await RunAsync(runner, root, ["add", "app.txt"], cancellationToken);
            await RunAsync(runner, root, ["commit", "-m", "initial"], cancellationToken);

            await File.WriteAllTextAsync(Path.Combine(root, "app.txt"), "changed\n", cancellationToken);
            var service = new GitMutationService(runner);

            await service.StageAsync(root, ["app.txt"], cancellationToken);
            var staged = await runner.RunAsync(root, ["diff", "--cached", "--name-only"], cancellationToken);
            Assert.Equal("app.txt", staged.StandardOutput.Trim());

            await service.UnstageAsync(root, ["app.txt"], cancellationToken);
            var unstaged = await runner.RunAsync(root, ["diff", "--cached", "--name-only"], cancellationToken);
            Assert.True(string.IsNullOrWhiteSpace(unstaged.StandardOutput));

            await service.StageAsync(root, ["app.txt"], cancellationToken);
            var sha = await service.CommitStagedAsync(root, "real workbench commit", cancellationToken);
            var subject = await runner.RunAsync(root, ["log", "-1", "--format=%s"], cancellationToken);

            Assert.Matches("^[0-9a-f]{40}$", sha);
            Assert.Equal("real workbench commit", subject.StandardOutput.Trim());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task StageAsync_RejectsPathTraversalBeforeGitMutation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), $"rgd-mutation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var runner = new GitProcessRunner();
            await RunAsync(runner, root, ["init", "-b", "main"], cancellationToken);
            var service = new GitMutationService(runner);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.StageAsync(root, ["../outside.txt"], cancellationToken));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static async Task RunAsync(GitProcessRunner runner, string root, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(root, arguments, cancellationToken);
        Assert.True(result.ExitCode == 0, result.StandardError);
    }
}
