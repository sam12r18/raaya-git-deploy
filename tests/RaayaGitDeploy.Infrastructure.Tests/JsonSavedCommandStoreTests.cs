using RaayaGitDeploy.Core.Commands;
using RaayaGitDeploy.Infrastructure.Commands;

namespace RaayaGitDeploy.Infrastructure.Tests;

public sealed class JsonSavedCommandStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "raaya-git-deploy-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task UpsertLoadDelete_PersistsCommandsOutsideRepository()
    {
        Directory.CreateDirectory(_root);
        var filePath = Path.Combine(_root, "commands.json");
        var store = new JsonSavedCommandStore(filePath);
        var command = new SavedCommand("build", "Build", "dotnet build", null);
        var cancellationToken = TestContext.Current.CancellationToken;

        await store.UpsertAsync(command, cancellationToken);
        var loaded = await store.LoadAsync(cancellationToken);

        var saved = Assert.Single(loaded);
        Assert.Equal(command, saved);
        Assert.Equal(filePath, store.StoragePath);

        await store.DeleteAsync(command.Id, cancellationToken);
        Assert.Empty(await store.LoadAsync(cancellationToken));
    }

    [Fact]
    public async Task Upsert_ReplacesExistingCommandWithSameId()
    {
        Directory.CreateDirectory(_root);
        var store = new JsonSavedCommandStore(Path.Combine(_root, "commands.json"));
        var cancellationToken = TestContext.Current.CancellationToken;
        await store.UpsertAsync(new SavedCommand("test", "Test", "dotnet test", null), cancellationToken);

        await store.UpsertAsync(new SavedCommand("test", "Test all", "dotnet test RaayaGitDeploy.slnx", @"C:\work"), cancellationToken);

        var saved = Assert.Single(await store.LoadAsync(cancellationToken));
        Assert.Equal("Test all", saved.Name);
        Assert.Equal("dotnet test RaayaGitDeploy.slnx", saved.CommandText);
        Assert.Equal(@"C:\work", saved.WorkingDirectoryOverride);
    }

    [Fact]
    public async Task Load_WhenFileIsMalformed_RecoversWithEmptyCollectionAndPreservesCorruptCopy()
    {
        Directory.CreateDirectory(_root);
        var filePath = Path.Combine(_root, "commands.json");
        await File.WriteAllTextAsync(filePath, "{not-json", TestContext.Current.CancellationToken);
        var store = new JsonSavedCommandStore(filePath);

        var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(loaded);
        Assert.False(File.Exists(filePath));
        Assert.Single(Directory.GetFiles(_root, "commands.json.corrupt-*"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
