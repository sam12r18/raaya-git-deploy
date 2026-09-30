namespace RaayaGitDeploy.Android.Core.Security;

/// <summary>
/// Owns the companion session boundary. Only the short-lived agent/API access token is
/// persisted through the platform-protected token store; transport and Git credentials
/// never cross this boundary.
/// </summary>
public sealed class CompanionSessionLifecycle(IAccessTokenStore accessTokenStore)
{
    public async Task<bool> RestoreAsync(CancellationToken cancellationToken)
    {
        var token = await accessTokenStore.GetAccessTokenAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(token);
    }

    public Task SignInAsync(string accessToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        return accessTokenStore.SaveAccessTokenAsync(accessToken, cancellationToken);
    }

    public Task SignOutAsync(CancellationToken cancellationToken) =>
        accessTokenStore.ClearAsync(cancellationToken);
}
