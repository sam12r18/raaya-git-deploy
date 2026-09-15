using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitRepositoryServiceWindowsPathTests
{
    [Fact]
    public async Task GetDiffAsync_UntrackedTextFileWithWindowsSeparators_ReturnsPreview()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), $"rgd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "docs"));

        try
        {
            var runner = new GitProcessRunner();
            await runner.RunAsync(root, new[] { "init", "-b", "main" }, cancellationToken);
            await runner.RunAsync(root, new[] { "config", "user.email", "test@example.invalid" }, cancellationToken);
            await runner.RunAsync(root, new[] { "config", "user.name", "Raaya Test" }, cancellationToken);

            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "# test\n", cancellationToken);
            await runner.RunAsync(root, new[] { "add", "README.md" }, cancellationToken);
            await runner.RunAsync(root, new[] { "commit", "-m", "baseline" }, cancellationToken);

            await File.WriteAllTextAsync(
                Path.Combine(root, "docs", "notes.txt"),
                "windows path preview\n",
                cancellationToken);

            var service = new GitRepositoryService(runner);

            var diff = await service.GetDiffAsync(
                root,
                @"docs\notes.txt",
                null,
                cancellationToken);

            Assert.Contains("+++ b/docs/notes.txt", diff, StringComparison.Ordinal);
            Assert.Contains("+windows path preview", diff, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);

        foreach (var directory in Directory
                     .EnumerateDirectories(path, "*", SearchOption.AllDirectories)
                     .OrderByDescending(static directory => directory.Length))
            File.SetAttributes(directory, FileAttributes.Normal);

        File.SetAttributes(path, FileAttributes.Normal);
        Directory.Delete(path, recursive: true);
    }
}
