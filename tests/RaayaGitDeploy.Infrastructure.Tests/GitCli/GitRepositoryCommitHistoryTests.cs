using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitRepositoryCommitHistoryTests
{
    [Fact]
    public async Task GetRecentCommitsAsync_ReturnsNewestFirst_WithUnicodeMetadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), $"rgd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var runner = new GitProcessRunner();
            await runner.RunAsync(root, ["init", "-b", "main"], cancellationToken);
            await runner.RunAsync(root, ["config", "user.email", "test@example.invalid"], cancellationToken);
            await runner.RunAsync(root, ["config", "user.name", "سروش تست"], cancellationToken);

            await File.WriteAllTextAsync(Path.Combine(root, "one.txt"), "one\n", cancellationToken);
            await runner.RunAsync(root, ["add", "one.txt"], cancellationToken);
            await runner.RunAsync(root, ["commit", "-m", "اولین کامیت"], cancellationToken);

            await File.WriteAllTextAsync(Path.Combine(root, "two.txt"), "two\n", cancellationToken);
            await runner.RunAsync(root, ["add", "two.txt"], cancellationToken);
            await runner.RunAsync(root, ["commit", "-m", "second | subject % marker"], cancellationToken);

            var service = new GitRepositoryService(runner);
            var commits = await service.GetRecentCommitsAsync(root, 2, cancellationToken);

            Assert.Equal(2, commits.Count);
            Assert.Equal("second | subject % marker", commits[0].Subject);
            Assert.Equal("اولین کامیت", commits[1].Subject);
            Assert.Equal("سروش تست", commits[0].AuthorName);
            Assert.Matches("^[0-9a-f]{40}$", commits[0].Sha);
            Assert.Equal(commits[0].Sha[..8], commits[0].ShortSha);
            Assert.NotEqual(default, commits[0].AuthorDate);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task GetRecentCommitsAsync_RejectsLimitOutsideSupportedRange(int limit)
    {
        var service = new GitRepositoryService(new GitProcessRunner());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetRecentCommitsAsync(Path.GetTempPath(), limit, TestContext.Current.CancellationToken));
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
