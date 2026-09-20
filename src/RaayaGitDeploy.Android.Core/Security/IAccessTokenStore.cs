namespace RaayaGitDeploy.Android.Core.Security;

/// <summary>
/// Platform boundary for session material. The Android implementation must use Android Keystore-backed storage;
/// callers must never persist raw deployment, SSH, or Git credentials.
/// </summary>
public interface IAccessTokenStore
{
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken);
    Task SaveAccessTokenAsync(string accessToken, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
