using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.Presentation.Tests;

public sealed class HostProfileEditorViewModelTests
{
    [Fact]
    public void CreateProfile_RejectsMissingKeyReference()
    {
        var sut = CreateSut();
        sut.DisplayName = "Production";
        sut.Host = "example.test";
        sut.Username = "deploy";
        sut.RemoteRoot = "/home/deploy/app";

        var exception = Assert.Throws<ArgumentException>(() => sut.CreateProfile());

        Assert.Contains("key reference", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateProfile_RejectsLegacyCredentialReference()
    {
        var sut = CreateSut();
        sut.DisplayName = "Production";
        sut.Host = "example.test";
        sut.Username = "deploy";
        sut.RemoteRoot = "/home/deploy/app";
        sut.KeyReference = "credential://ssh/prod";

        var exception = Assert.Throws<ArgumentException>(() => sut.CreateProfile());

        Assert.Contains("secret://", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateProfile_CreatesSshKeyProfileWithoutSecretMaterial()
    {
        var sut = CreateSut();
        sut.DisplayName = "Production";
        sut.Host = "example.test";
        sut.Port = 2222;
        sut.Username = "deploy";
        sut.RemoteRoot = "/home/deploy/app";
        sut.KeyReference = "secret://host/prod";

        var profile = sut.CreateProfile("prod");

        Assert.Equal("prod", profile.Id);
        Assert.Equal(ServerAuthenticationMode.SshKey, profile.AuthenticationMode);
        Assert.Equal("secret://host/prod", profile.KeyReference);
    }

    [Fact]
    public async Task ImportPrivateKeyAsync_StoresSecretAndExposesOnlyReference()
    {
        var secrets = new StubSecretStore();
        var sut = CreateSut(secrets);
        const string privateKey = "-----BEGIN OPENSSH PRIVATE KEY-----\nabc123\n-----END OPENSSH PRIVATE KEY-----";

        var reference = await sut.ImportPrivateKeyAsync(privateKey, "production", CancellationToken.None);

        Assert.Equal("secret://host/production", reference.ToString());
        Assert.Equal(reference.ToString(), sut.KeyReference);
        Assert.Equal(privateKey, secrets.Secrets[reference.ToString()]);
        Assert.DoesNotContain("PRIVATE KEY", sut.KeyReference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportPrivateKeyAsync_RejectsNonKeyContentWithoutPersistingIt()
    {
        var secrets = new StubSecretStore();
        var sut = CreateSut(secrets);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.ImportPrivateKeyAsync("not a private key", "invalid", CancellationToken.None));

        Assert.Empty(secrets.Secrets);
        Assert.Equal(string.Empty, sut.KeyReference);
    }

    private static HostProfileEditorViewModel CreateSut(StubSecretStore? secrets = null)
    {
        return new HostProfileEditorViewModel(
            new ServersViewModel(new StubStore(), new StubTransport()),
            secrets ?? new StubSecretStore());
    }

    private sealed class StubSecretStore : ISecretStore
    {
        public Dictionary<string, string> Secrets { get; } = new(StringComparer.Ordinal);

        public Task SetAsync(SecretReference reference, string secret, CancellationToken cancellationToken)
        {
            Secrets[reference.ToString()] = secret;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(SecretReference reference, CancellationToken cancellationToken) =>
            Task.FromResult(Secrets.GetValueOrDefault(reference.ToString()));

        public Task DeleteAsync(SecretReference reference, CancellationToken cancellationToken)
        {
            Secrets.Remove(reference.ToString());
            return Task.CompletedTask;
        }
    }

    private sealed class StubStore : IServerProfileStore
    {
        public Task<IReadOnlyList<ServerProfile>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ServerProfile>>([]);
        public Task UpsertAsync(ServerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubTransport : IRemoteTransport
    {
        public Task TestConnectionAsync(ServerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task UploadAsync(ServerProfile profile, string localPath, string remotePath, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
