using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.Presentation.Tests.Deployment;

public sealed class ServersViewModelTests
{
    [Fact]
    public async Task Load_selects_first_profile_without_testing_connection()
    {
        var profile = Profile("primary", "Primary");
        var store = new FakeStore(profile);
        var transport = new FakeTransport();
        var sut = new ServersViewModel(store, transport);

        await sut.LoadAsync(CancellationToken.None);

        Assert.Single(sut.Profiles);
        Assert.Equal(profile, sut.SelectedProfile);
        Assert.Equal(0, transport.TestConnectionCalls);
    }

    [Fact]
    public async Task Save_upserts_profile_and_refreshes_selection()
    {
        var store = new FakeStore();
        var sut = new ServersViewModel(store, new FakeTransport());
        var profile = Profile("staging", "Staging");

        await sut.SaveAsync(profile, CancellationToken.None);

        Assert.Equal(profile, store.Upserted);
        Assert.Equal(profile, sut.SelectedProfile);
        Assert.Contains(profile, sut.Profiles);
    }

    [Fact]
    public async Task Delete_removes_selected_profile_without_remote_operation()
    {
        var profile = Profile("old", "Old");
        var store = new FakeStore(profile);
        var transport = new FakeTransport();
        var sut = new ServersViewModel(store, transport);
        await sut.LoadAsync(CancellationToken.None);

        await sut.DeleteSelectedAsync(CancellationToken.None);

        Assert.Equal("old", store.DeletedId);
        Assert.Empty(sut.Profiles);
        Assert.Null(sut.SelectedProfile);
        Assert.Equal(0, transport.TestConnectionCalls);
    }

    [Fact]
    public async Task Test_connection_is_explicit_and_uses_selected_profile()
    {
        var profile = Profile("prod", "Production");
        var transport = new FakeTransport();
        var sut = new ServersViewModel(new FakeStore(profile), transport);
        await sut.LoadAsync(CancellationToken.None);

        await sut.TestSelectedConnectionAsync(CancellationToken.None);

        Assert.Equal(1, transport.TestConnectionCalls);
        Assert.Equal(profile, transport.LastTestedProfile);
    }

    private static ServerProfile Profile(string id, string name) =>
        new(id, name, "example.test", 22, "deploy", "/var/www/app", ServerAuthenticationMode.SshKey, "key-ref");

    private sealed class FakeStore(params ServerProfile[] profiles) : IServerProfileStore
    {
        private readonly List<ServerProfile> _profiles = [.. profiles];
        public ServerProfile? Upserted { get; private set; }
        public string? DeletedId { get; private set; }

        public Task<IReadOnlyList<ServerProfile>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ServerProfile>>(_profiles.ToArray());

        public Task UpsertAsync(ServerProfile profile, CancellationToken cancellationToken)
        {
            Upserted = profile;
            _profiles.RemoveAll(item => item.Id == profile.Id);
            _profiles.Add(profile);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken cancellationToken)
        {
            DeletedId = id;
            _profiles.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTransport : IRemoteTransport
    {
        public int TestConnectionCalls { get; private set; }
        public ServerProfile? LastTestedProfile { get; private set; }

        public Task TestConnectionAsync(ServerProfile profile, CancellationToken cancellationToken)
        {
            TestConnectionCalls++;
            LastTestedProfile = profile;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task UploadAsync(ServerProfile profile, string localPath, string remotePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task DeleteAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
