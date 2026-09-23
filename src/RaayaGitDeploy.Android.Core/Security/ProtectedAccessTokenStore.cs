namespace RaayaGitDeploy.Android.Core.Security;

/// <summary>
/// Access-token store that ensures only platform-protected bytes reach persistence.
/// Compose this behind <see cref="ValidatedAccessTokenStore"/> so malformed session material
/// is rejected before encryption. The platform protector is where Android Keystore integration lives.
/// </summary>
public sealed class ProtectedAccessTokenStore(
    IPlatformAccessTokenProtector protector,
    IProtectedAccessTokenPersistence persistence) : IAccessTokenStore
{
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var protectedToken = await persistence.ReadAsync(cancellationToken);
        if (protectedToken is null || protectedToken.Length == 0) return null;

        try
        {
            return await protector.UnprotectAsync(protectedToken, cancellationToken);
        }
        catch
        {
            // Corrupt, stale, or no-longer-decryptable session material must not escape the
            // secure-storage boundary. Clear it so callers fall back to re-authentication.
            await persistence.ClearAsync(cancellationToken);
            return null;
        }
    }

    public async Task SaveAccessTokenAsync(string accessToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);
        var protectedToken = await protector.ProtectAsync(accessToken, cancellationToken);
        if (protectedToken.Length == 0)
            throw new InvalidOperationException("Platform token protection returned an empty payload.");

        await persistence.WriteAsync(protectedToken, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken) => persistence.ClearAsync(cancellationToken);
}
