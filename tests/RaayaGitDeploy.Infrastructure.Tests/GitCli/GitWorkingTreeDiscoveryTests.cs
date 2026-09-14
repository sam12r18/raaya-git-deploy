using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitWorkingTreeDiscoveryTests
{
    [Fact]
    public async Task GetWorkingTreeChangesAsync_ReturnsSupportedChangeKinds()
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

            await File.WriteAllTextAsync(Path.Combine(root, "modified.txt"), "before", cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "deleted.txt"), "delete me", cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "rename-old.txt"), "rename me", cancellationToken);
            await runner.RunAsync(root, new[] { "add", "." }, cancellationToken);
            await runner.RunAsync(root, new[] { "commit", "-m", "baseline" }, cancellationToken);

            await File.WriteAllTextAsync(Path.Combine(root, "modified.txt"), "after", cancellationToken);
            File.Delete(Path.Combine(root, "deleted.txt"));
            await runner.RunAsync(root, new[] { "mv", "rename-old.txt", "rename-new.txt" }, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "added.txt"), "added", cancellationToken);
            await runner.RunAsync(root, new[] { "add", "added.txt" }, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(root, "untracked.txt"), "untracked", cancellationToken);

            var service = new GitRepositoryService(runner);

            var changes = await service.GetWorkingTreeChangesAsync(root, cancellationToken);

            Assert.Contains(changes, change =>
                change.Path == "modified.txt" && change.Kind == GitChangeKind.Modified);
            Assert.Contains(changes, change =>
                change.Path == "deleted.txt" && change.Kind == GitChangeKind.Deleted);
            Assert.Contains(changes, change =>
                change.Path == "rename-new.txt" &&
                change.OriginalPath == "rename-old.txt" &&
                change.Kind == GitChangeKind.Renamed);
            Assert.Contains(changes, change =>
                change.Path == "added.txt" && change.Kind == GitChangeKind.Added);
            Assert.Contains(changes, change =>
                change.Path == "untracked.txt" && change.Kind == GitChangeKind.Untracked);
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
