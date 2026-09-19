using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.Presentation.Tests;

public sealed class HostProfileEditorViewModelTests
{
    [Fact]
    public void CreateProfile_RejectsRawMissingKeyReference()
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
    public void CreateProfile_CreatesSshKeyProfileWithoutSecretMaterial()
    {
        var sut = CreateSut();
        sut.DisplayName = "Production";
        sut.Host = "example.test";
        sut.Port = 2222;
        sut.Username = "deploy";
        sut.RemoteRoot = "/home/deploy/app";
        sut.KeyReference = "credential://ssh/prod";

        var profile = sut.CreateProfile("prod");

        Assert.Equal("prod", profile.Id);
        Assert.Equal(ServerAuthenticationMode.SshKey, profile.AuthenticationMode);
        Assert.Equal("credential://ssh/prod", profile.KeyReference);
    }

    private static HostProfileEditorViewModel CreateSut()
    {
        return new HostProfileEditorViewModel(new ServersViewModel(new StubStore(), new StubTransport()));
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
        public Task UploadFileAsync(ServerProfile profile, string localPath, string remotePath, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ExecuteCommandAsync(ServerProfile profile, string command, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
