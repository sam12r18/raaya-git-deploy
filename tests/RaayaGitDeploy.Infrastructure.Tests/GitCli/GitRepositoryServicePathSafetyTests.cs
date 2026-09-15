using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitRepositoryServicePathSafetyTests
{
    [Fact]
    public async Task GetDiffAsync_PathOutsideRepository_ThrowsBeforeReadingFile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var parent = Path.Combine(Path.GetTempPath(), $"rgd-path-{Guid.NewGuid():N}");
        var root = Path.Combine(parent, "repo");
        Directory.CreateDirectory(root);

        try
        {
            var runner = new GitProcessRunner();
            await runner.RunAsync(root, new[] { "init", "-b", "main" }, cancellationToken);
            await runner.RunAsync(root, new[] { "config", "user.email", "test@example.invalid" }, cancellationToken);
            await runner.RunAsync(root, new[] { "config", "user.name", "Raaya Test" }, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# test\n", cancellationToken);
            await runner.RunAsync(root, new[] { "add", "README.md" }, cancellationToken);
            await runner.RunAsync(root, new[] { "commit", "-m", "baseline" }, cancellationToken);

            var outsidePath = Path.Combine(parent, "outside.txt");
            await File.WriteAllTextAsync(outsidePath, "must not be previewed\n", cancellationToken);

            var service = new GitRepositoryService(runner);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.GetDiffAsync(root, "../outside.txt", null, cancellationToken));
        }
        finally
        {
            if (Directory.Exists(parent))
            {
                foreach (var file in Directory.EnumerateFiles(parent, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(parent, recursive: true);
            }
        }
    }
}
