using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitRepositoryServicePreviewLimitTests
{
    [Fact]
    public async Task GetDiffAsync_UntrackedTextFileAboveDefaultPreviewLimit_ReturnsEmpty()
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

            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# test\n", cancellationToken);
            await runner.RunAsync(root, new[] { "add", "README.md" }, cancellationToken);
            await runner.RunAsync(root, new[] { "commit", "-m", "baseline" }, cancellationToken);

            const string relativePath = "large-untracked.txt";
            var content = new string('x', 1_048_577);
            await File.WriteAllTextAsync(Path.Combine(root, relativePath), content, cancellationToken);

            var service = new GitRepositoryService(runner);

            var diff = await service.GetDiffAsync(root, relativePath, null, cancellationToken);

            Assert.Equal(string.Empty, diff);
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
