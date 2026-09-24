using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Infrastructure.Deployment;

namespace RaayaGitDeploy.Infrastructure.Tests;

public sealed class JsonDeploymentHistoryStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "raaya-git-deploy-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_reads_legacy_entry_without_git_metadata()
    {
        Directory.CreateDirectory(_root);
        var filePath = Path.Combine(_root, "history.json");
        var cancellationToken = TestContext.Current.CancellationToken;

        await File.WriteAllTextAsync(
            filePath,
            """
            [{
              "id":"legacy-1",
              "startedAt":"2026-09-20T10:00:00+00:00",
              "serverProfileId":"prod",
              "serverDisplayName":"Production",
              "succeeded":true,
              "items":[]
            }]
            """,
            cancellationToken);

        var entries = await new JsonDeploymentHistoryStore(filePath)
            .LoadAsync(cancellationToken);

        var entry = Assert.Single(entries);
        Assert.Equal("legacy-1", entry.Id);
        Assert.Null(entry.RepositoryPath);
        Assert.Null(entry.Branch);
        Assert.Null(entry.FromHead);
        Assert.Null(entry.ToHead);
        Assert.Null(entry.FinishedAt);
    }

    [Fact]
    public async Task AppendAsync_round_trips_git_baseline_metadata()
    {
        Directory.CreateDirectory(_root);
        var filePath = Path.Combine(_root, "history.json");
        var store = new JsonDeploymentHistoryStore(filePath);
        var cancellationToken = TestContext.Current.CancellationToken;
        var startedAt = DateTimeOffset.Parse("2026-09-24T12:00:00Z");
        var finishedAt = startedAt.AddMinutes(2);
        var entry = new DeploymentHistoryEntry(
            "deploy-1",
            startedAt,
            "prod",
            "Production",
            true,
            [],
            @"C:\work\app",
            "main",
            "aaaaaaaa",
            "bbbbbbbb",
            finishedAt);

        await store.AppendAsync(entry, cancellationToken);
        var loaded = Assert.Single(await store.LoadAsync(cancellationToken));

        Assert.Equal(@"C:\work\app", loaded.RepositoryPath);
        Assert.Equal("main", loaded.Branch);
        Assert.Equal("aaaaaaaa", loaded.FromHead);
        Assert.Equal("bbbbbbbb", loaded.ToHead);
        Assert.Equal(finishedAt, loaded.FinishedAt);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
