using Android.Content;
using RaayaGitDeploy.Android.Core.Security;

namespace RaayaGitDeploy.Android.Security;

/// <summary>
/// Composes the Mobile session boundary so callers cannot accidentally persist a raw token.
/// Validation happens before Keystore protection; persistence receives encrypted bytes only.
/// </summary>
public static class AndroidAccessTokenStoreFactory
{
    public static IAccessTokenStore Create(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var protectedStore = new ProtectedAccessTokenStore(
            new AndroidKeystoreAccessTokenProtector(),
            new AndroidProtectedAccessTokenPersistence(context));
        return new ValidatedAccessTokenStore(protectedStore);
    }
}
