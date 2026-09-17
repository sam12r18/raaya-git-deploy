using System.Diagnostics;
using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitRepositoryCommitDetailsTests : IDisposable
{
    private readonly string _repositoryPath = Path.Combine(Path.GetTempPath(), $"raaya-git-details-{Guid.NewGuid():N}");

    [Fact]
    public async Task GetCommitChangesAsync_ReturnsAddedModifiedDeletedAndRenamedFiles()
    {
        Directory.CreateDirectory(_repositoryPath);
        RunGit("init");
        RunGit("config", "user.name", "Commit Detail Tester");
        RunGit("config", "user.email", "details@example.test");

        File.WriteAllText(Path.Combine(_repositoryPath, "modified.txt"), "before\n");
        File.WriteAllText(Path.Combine(_repositoryPath, "deleted.txt"), "delete me\n");
        File.WriteAllText(Path.Combine(_repositoryPath, "old-name.txt"), "rename me\n");
        RunGit("add", ".");
        RunGit("commit", "-m", "baseline");

        File.WriteAllText(Path.Combine(_repositoryPath, "modified.txt"), "after\n");
        File.Delete(Path.Combine(_repositoryPath, "deleted.txt"));
        File.Move(Path.Combine(_repositoryPath, "old-name.txt"), Path.Combine(_repositoryPath, "new-name.txt"));
        File.WriteAllText(Path.Combine(_repositoryPath, "added.txt"), "added\n");
        RunGit("add", "-A");
        RunGit("commit", "-m", "details");
        var commitSha = RunGit("rev-parse", "HEAD").Trim();

        var service = new GitRepositoryService(new GitProcessRunner());
        var changes = await service.GetCommitChangesAsync(_repositoryPath, commitSha, CancellationToken.None);

        Assert.Contains(changes, change => change.Kind == GitChangeKind.Added && change.Path == "added.txt");
        Assert.Contains(changes, change => change.Kind == GitChangeKind.Modified && change.Path == "modified.txt");
        Assert.Contains(changes, change => change.Kind == GitChangeKind.Deleted && change.Path == "deleted.txt");
        Assert.Contains(changes, change => change.Kind == GitChangeKind.Renamed && change.Path == "new-name.txt" && change.OriginalPath == "old-name.txt");
    }

    [Fact]
    public async Task GetCommitFileDiffAsync_ReturnsOnlyRequestedCommitFile()
    {
        Directory.CreateDirectory(_repositoryPath);
        RunGit("init");
        RunGit("config", "user.name", "Commit Detail Tester");
        RunGit("config", "user.email", "details@example.test");
        File.WriteAllText(Path.Combine(_repositoryPath, "safe.txt"), "before\n");
        RunGit("add", ".");
        RunGit("commit", "-m", "baseline");
        File.WriteAllText(Path.Combine(_repositoryPath, "safe.txt"), "after\n");
        RunGit("add", ".");
        RunGit("commit", "-m", "change safe file");
        var commitSha = RunGit("rev-parse", "HEAD").Trim();

        var service = new GitRepositoryService(new GitProcessRunner());
        var diff = await service.GetCommitFileDiffAsync(_repositoryPath, commitSha, "safe.txt", CancellationToken.None);

        Assert.Contains("diff --git a/safe.txt b/safe.txt", diff);
        Assert.Contains("+after", diff);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetCommitFileDiffAsync(_repositoryPath, commitSha, "../outside.txt", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetCommitChangesAsync(_repositoryPath, "--help", CancellationToken.None));
    }

    private string RunGit(params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = _repositoryPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
        return output;
    }

    public void Dispose()
    {
        if (!Directory.Exists(_repositoryPath)) return;

        // Git for Windows can briefly retain handles after a child process exits.
        // Retry cleanup so a successful integration assertion is not reported as a test failure.
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                Directory.Delete(_repositoryPath, recursive: true);
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                Thread.Sleep(100 * attempt);
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(100 * attempt);
            }
        }

        Directory.Delete(_repositoryPath, recursive: true);
    }
}
