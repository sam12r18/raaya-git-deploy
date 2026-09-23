namespace RaayaGitDeploy.Android.Core.Security;

/// <summary>
/// Validates short-lived companion session material before it reaches platform storage.
/// The inner Android implementation is expected to be Keystore-backed.
/// </summary>
public sealed class ValidatedAccessTokenStore(IAccessTokenStore inner) : IAccessTokenStore
{
    public const int MaximumTokenLength = 8192;

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var token = await inner.GetAccessTokenAsync(cancellationToken);
        if (token is null) return null;
        return IsValid(token) ? token : null;
    }

    public Task SaveAccessTokenAsync(string accessToken, CancellationToken cancellationToken)
    {
        if (!IsValid(accessToken))
            throw new ArgumentException("Access token is malformed and will not be persisted.", nameof(accessToken));

        return inner.SaveAccessTokenAsync(accessToken, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken) => inner.ClearAsync(cancellationToken);

    public static bool IsValid(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > MaximumTokenLength) return false;
        return !token.Any(char.IsWhiteSpace) && !token.Any(char.IsControl);
    }
}
