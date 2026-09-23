using RaayaGitDeploy.Android.Core.Security;

namespace RaayaGitDeploy.Android.Core.Tests.Security;

public sealed class ValidatedAccessTokenStoreTests
{
    [Fact]
    public async Task SaveAccessTokenAsync_ValidToken_ReachesPlatformStore()
    {
        var inner = new RecordingStore();
        var store = new ValidatedAccessTokenStore(inner);

        await store.SaveAccessTokenAsync("short-lived-session", CancellationToken.None);

        Assert.Equal("short-lived-session", inner.SavedToken);
    }

    [Theory]
    [InlineData("")]
    [InlineData("token with spaces")]
    [InlineData("token\nwith-control")]
    public async Task SaveAccessTokenAsync_MalformedToken_NeverReachesPlatformStore(string token)
    {
        var inner = new RecordingStore();
        var store = new ValidatedAccessTokenStore(inner);

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAccessTokenAsync(token, CancellationToken.None));

        Assert.Null(inner.SavedToken);
    }

    [Fact]
    public async Task GetAccessTokenAsync_MalformedPersistedToken_IsNotReturned()
    {
        var inner = new RecordingStore { StoredToken = "bad token" };
        var store = new ValidatedAccessTokenStore(inner);

        Assert.Null(await store.GetAccessTokenAsync(CancellationToken.None));
    }

    private sealed class RecordingStore : IAccessTokenStore
    {
        public string? StoredToken { get; set; }
        public string? SavedToken { get; private set; }

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult(StoredToken);
        public Task SaveAccessTokenAsync(string accessToken, CancellationToken cancellationToken)
        {
            SavedToken = accessToken;
            return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken cancellationToken)
        {
            StoredToken = null;
            return Task.CompletedTask;
        }
    }
}
