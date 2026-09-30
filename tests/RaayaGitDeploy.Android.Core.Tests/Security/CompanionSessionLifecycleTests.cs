using RaayaGitDeploy.Android.Core.Security;

namespace RaayaGitDeploy.Android.Core.Tests.Security;

public sealed class CompanionSessionLifecycleTests
{
    [Fact]
    public async Task SignInAsync_PersistsOnlyThroughAccessTokenStore()
    {
        var store = new RecordingAccessTokenStore();
        var lifecycle = new CompanionSessionLifecycle(store);

        await lifecycle.SignInAsync("agent-session-token", CancellationToken.None);

        Assert.Equal("agent-session-token", store.SavedToken);
        Assert.Equal(1, store.SaveCalls);
    }

    [Fact]
    public async Task RestoreAsync_ReportsWhetherProtectedSessionExists()
    {
        var store = new RecordingAccessTokenStore { StoredToken = "restored-token" };
        var lifecycle = new CompanionSessionLifecycle(store);

        Assert.True(await lifecycle.RestoreAsync(CancellationToken.None));

        store.StoredToken = null;
        Assert.False(await lifecycle.RestoreAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SignOutAsync_ClearsProtectedSession()
    {
        var store = new RecordingAccessTokenStore { StoredToken = "token" };
        var lifecycle = new CompanionSessionLifecycle(store);

        await lifecycle.SignOutAsync(CancellationToken.None);

        Assert.Equal(1, store.ClearCalls);
        Assert.Null(store.StoredToken);
    }

    private sealed class RecordingAccessTokenStore : IAccessTokenStore
    {
        public string? StoredToken { get; set; }
        public string? SavedToken { get; private set; }
        public int SaveCalls { get; private set; }
        public int ClearCalls { get; private set; }

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken) =>
            Task.FromResult(StoredToken);

        public Task SaveAccessTokenAsync(string accessToken, CancellationToken cancellationToken)
        {
            SaveCalls++;
            SavedToken = accessToken;
            StoredToken = accessToken;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            ClearCalls++;
            StoredToken = null;
            return Task.CompletedTask;
        }
    }
}
