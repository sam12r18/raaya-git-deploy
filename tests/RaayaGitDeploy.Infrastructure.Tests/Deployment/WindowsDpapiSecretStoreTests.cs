using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Infrastructure.Deployment;

namespace RaayaGitDeploy.Infrastructure.Tests.Deployment;

public sealed class WindowsDpapiSecretStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "RaayaGitDeploy.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RoundTrip_PersistsOnlyProtectedBlob()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store = new WindowsDpapiSecretStore(_directory);
        var reference = SecretReference.Create("ssh", "production-key");
        const string secret = "private-key-material-that-must-not-be-plaintext";

        await store.SetAsync(reference, secret, CancellationToken.None);

        Assert.Equal(secret, await store.GetAsync(reference, CancellationToken.None));
        var persisted = await File.ReadAllBytesAsync(Assert.Single(Directory.GetFiles(_directory)));
        Assert.DoesNotContain(secret, Convert.ToBase64String(persisted), StringComparison.Ordinal);
        Assert.DoesNotContain("production-key", Path.GetFileName(Assert.Single(Directory.GetFiles(_directory))), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_RemovesProtectedBlob()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store = new WindowsDpapiSecretStore(_directory);
        var reference = SecretReference.Create("ssh", "temporary-key");
        await store.SetAsync(reference, "secret", CancellationToken.None);

        await store.DeleteAsync(reference, CancellationToken.None);

        Assert.Null(await store.GetAsync(reference, CancellationToken.None));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
