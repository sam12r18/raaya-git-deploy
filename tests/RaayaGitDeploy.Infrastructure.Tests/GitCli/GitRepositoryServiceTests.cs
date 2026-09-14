using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitRepositoryServiceTests
{
    [Fact]
    public async Task GetContextAsync_ReturnsRootBranchAndHead()
    {
        var root = Path.Combine(Path.GetTempPath(), $"rgd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var runner = new GitProcessRunner();
            await runner.RunAsync(root, new[] { "init", "-b", "main" }, CancellationToken.None);
            await runner.RunAsync(root, new[] { "config", "user.email", "test@example.invalid" }, CancellationToken.None);
            await runner.RunAsync(root, new[] { "config", "user.name", "Raaya Test" }, CancellationToken.None);

            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# test");
            await runner.RunAsync(root, new[] { "add", "README.md" }, CancellationToken.None);
            await runner.RunAsync(root, new[] { "commit", "-m", "initial" }, CancellationToken.None);

            var service = new GitRepositoryService(runner);

            var context = await service.GetContextAsync(root, CancellationToken.None);

            Assert.Equal(
                Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar),
                context.RootPath.TrimEnd(Path.DirectorySeparatorChar));
            Assert.Equal("main", context.BranchName);
            Assert.Matches("^[0-9a-f]{40}$", context.HeadSha);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
