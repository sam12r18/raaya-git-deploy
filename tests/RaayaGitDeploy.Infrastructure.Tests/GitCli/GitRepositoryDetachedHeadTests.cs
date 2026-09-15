using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitRepositoryDetachedHeadTests
{
    [Fact]
    public async Task GetContextAsync_DetachedHead_UsesAbbreviatedHeadLabel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), $"rgd-detached-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var runner = new GitProcessRunner();
            await RunRequiredAsync(runner, root, ["init", "-b", "main"], cancellationToken);
            await RunRequiredAsync(runner, root, ["config", "user.email", "test@example.invalid"], cancellationToken);
            await RunRequiredAsync(runner, root, ["config", "user.name", "Raaya Test"], cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# detached test\n", cancellationToken);
            await RunRequiredAsync(runner, root, ["add", "README.md"], cancellationToken);
            await RunRequiredAsync(runner, root, ["commit", "-m", "initial"], cancellationToken);

            var head = (await RunRequiredAsync(runner, root, ["rev-parse", "HEAD"], cancellationToken)).Trim();
            await RunRequiredAsync(runner, root, ["checkout", "--detach", "HEAD"], cancellationToken);

            var service = new GitRepositoryService(runner);
            var context = await service.GetContextAsync(root, cancellationToken);

            Assert.Equal(head, context.HeadSha);
            Assert.Equal($"detached@{head[..8]}", context.BranchName);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static async Task<string> RunRequiredAsync(
        GitProcessRunner runner,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(workingDirectory, arguments, cancellationToken);
        Assert.Equal(0, result.ExitCode);
        return result.StandardOutput;
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        foreach (var directory in Directory
                     .EnumerateDirectories(path, "*", SearchOption.AllDirectories)
                     .OrderByDescending(static directory => directory.Length))
        {
            File.SetAttributes(directory, FileAttributes.Normal);
        }

        File.SetAttributes(path, FileAttributes.Normal);
        Directory.Delete(path, recursive: true);
    }
}
