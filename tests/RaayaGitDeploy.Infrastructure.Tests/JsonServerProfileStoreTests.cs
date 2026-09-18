using System.Text.Json;
using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Infrastructure.Deployment;

namespace RaayaGitDeploy.Infrastructure.Tests;

public sealed class JsonServerProfileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "raaya-git-deploy-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task UpsertLoadDelete_PersistsNonSecretServerConfiguration()
    {
        Directory.CreateDirectory(_root);
        var filePath = Path.Combine(_root, "servers.json");
        var store = new JsonServerProfileStore(filePath);
        var profile = new ServerProfile(
            "production",
            "Production",
            "deploy.example.com",
            22,
            "deployer",
            "/var/www/app",
            ServerAuthenticationMode.SshKey,
            "windows-credential:raaya-git-deploy/production");
        var cancellationToken = TestContext.Current.CancellationToken;

        await store.UpsertAsync(profile, cancellationToken);
        var loaded = await store.LoadAsync(cancellationToken);

        Assert.Equal(profile, Assert.Single(loaded));
        Assert.Equal(filePath, store.StoragePath);

        await store.DeleteAsync(profile.Id, cancellationToken);
        Assert.Empty(await store.LoadAsync(cancellationToken));
    }

    [Fact]
    public async Task PersistedJson_ContainsKeyReferenceButNeverPlaintextPasswordField()
    {
        Directory.CreateDirectory(_root);
        var filePath = Path.Combine(_root, "servers.json");
        var store = new JsonServerProfileStore(filePath);
        var cancellationToken = TestContext.Current.CancellationToken;
        await store.UpsertAsync(
            new ServerProfile("staging", "Staging", "staging.example.com", 2222, "deploy", "/srv/app", ServerAuthenticationMode.SshKey, "key-ref-123"),
            cancellationToken);

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        using var document = JsonDocument.Parse(json);
        var persisted = Assert.Single(document.RootElement.EnumerateArray());

        Assert.Equal("key-ref-123", persisted.GetProperty("keyReference").GetString());
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Upsert_ReplacesExistingProfileWithSameId()
    {
        Directory.CreateDirectory(_root);
        var store = new JsonServerProfileStore(Path.Combine(_root, "servers.json"));
        var cancellationToken = TestContext.Current.CancellationToken;
        await store.UpsertAsync(
            new ServerProfile("prod", "Production", "old.example.com", 22, "deploy", "/old", ServerAuthenticationMode.SshKey, "old-key"),
            cancellationToken);

        await store.UpsertAsync(
            new ServerProfile("prod", "Production EU", "new.example.com", 2022, "release", "/new", ServerAuthenticationMode.SshKey, "new-key"),
            cancellationToken);

        var saved = Assert.Single(await store.LoadAsync(cancellationToken));
        Assert.Equal("Production EU", saved.DisplayName);
        Assert.Equal("new.example.com", saved.Host);
        Assert.Equal(2022, saved.Port);
        Assert.Equal("new-key", saved.KeyReference);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
