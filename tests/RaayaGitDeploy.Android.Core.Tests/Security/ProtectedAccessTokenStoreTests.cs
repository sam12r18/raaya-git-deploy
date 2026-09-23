using RaayaGitDeploy.Android.Core.Security;

namespace RaayaGitDeploy.Android.Core.Tests.Security;

public sealed class ProtectedAccessTokenStoreTests
{
    [Fact]
    public async Task Save_protects_token_before_persistence()
    {
        var protector = new FakeProtector();
        var persistence = new FakePersistence();
        var store = new ProtectedAccessTokenStore(protector, persistence);

        await store.SaveAccessTokenAsync("session-token", CancellationToken.None);

        Assert.Equal("session-token", protector.LastPlaintext);
        Assert.Equal([1, 2, 3], persistence.Stored);
    }

    [Fact]
    public async Task Read_unprotects_persisted_payload()
    {
        var protector = new FakeProtector { PlaintextToReturn = "session-token" };
        var persistence = new FakePersistence { Stored = [4, 5, 6] };
        var store = new ProtectedAccessTokenStore(protector, persistence);

        var token = await store.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal("session-token", token);
        Assert.Equal([4, 5, 6], protector.LastProtectedPayload);
    }

    [Fact]
    public async Task Corrupt_payload_is_cleared_and_requires_reauthentication()
    {
        var protector = new FakeProtector { ThrowOnUnprotect = true };
        var persistence = new FakePersistence { Stored = [9] };
        var store = new ProtectedAccessTokenStore(protector, persistence);

        var token = await store.GetAccessTokenAsync(CancellationToken.None);

        Assert.Null(token);
        Assert.True(persistence.WasCleared);
        Assert.Null(persistence.Stored);
    }

    private sealed class FakeProtector : IPlatformAccessTokenProtector
    {
        public string? LastPlaintext { get; private set; }
        public byte[]? LastProtectedPayload { get; private set; }
        public string PlaintextToReturn { get; init; } = "unused";
        public bool ThrowOnUnprotect { get; init; }

        public Task<byte[]> ProtectAsync(string accessToken, CancellationToken cancellationToken)
        {
            LastPlaintext = accessToken;
            return Task.FromResult<byte[]>([1, 2, 3]);
        }

        public Task<string> UnprotectAsync(byte[] protectedToken, CancellationToken cancellationToken)
        {
            LastProtectedPayload = protectedToken;
            if (ThrowOnUnprotect) throw new InvalidOperationException("corrupt ciphertext");
            return Task.FromResult(PlaintextToReturn);
        }
    }

    private sealed class FakePersistence : IProtectedAccessTokenPersistence
    {
        public byte[]? Stored { get; set; }
        public bool WasCleared { get; private set; }

        public Task<byte[]?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Stored);

        public Task WriteAsync(byte[] protectedToken, CancellationToken cancellationToken)
        {
            Stored = protectedToken;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            WasCleared = true;
            Stored = null;
            return Task.CompletedTask;
        }
    }
}
