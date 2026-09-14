using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitRepositoryServiceTests
{
    [Fact]
    public async Task GetContextAsync_ReturnsRootBranchAndHead()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), $"rgd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var runner = new GitProcessRunner();
            await runner.RunAsync(root, new[] { "init", "-b", "main" }, cancellationToken);
            await runner.RunAsync(root, new[] { "config", "user.email", "test@example.invalid" }, cancellationToken);
            await runner.RunAsync(root, new[] { "config", "user.name", "Raaya Test" }, cancellationToken);

            await File.WriteAllTextAsync(
                Path.Combine(root, "README.md"),
                "# test",
                cancellationToken);
            await runner.RunAsync(root, new[] { "add", "README.md" }, cancellationToken);
            await runner.RunAsync(root, new[] { "commit", "-m", "initial" }, cancellationToken);

            var service = new GitRepositoryService(runner);

            var context = await service.GetContextAsync(root, cancellationToken);

            Assert.Equal(
                Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar),
                context.RootPath.TrimEnd(Path.DirectorySeparatorChar));
            Assert.Equal("main", context.BranchName);
            Assert.Matches("^[0-9a-f]{40}$", context.HeadSha);
        }
        finally
        {
            DeleteDirectory(root);
        }
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
