namespace RaayaGitDeploy.Android.Core.Api;

/// <summary>
/// Security policy for the Android companion agent endpoint. Mobile must never send bearer session
/// material to plaintext HTTP endpoints or embed credentials in endpoint URLs.
/// </summary>
public static class CompanionEndpointPolicy
{
    public static Uri RequireSecureBaseAddress(Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        if (!baseAddress.IsAbsoluteUri)
            throw new InvalidOperationException("Companion agent endpoint must be an absolute URI.");
        if (!string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Companion agent endpoint must use HTTPS.");
        if (!string.IsNullOrEmpty(baseAddress.UserInfo))
            throw new InvalidOperationException("Companion agent endpoint must not contain embedded credentials.");
        if (!string.IsNullOrEmpty(baseAddress.Query) || !string.IsNullOrEmpty(baseAddress.Fragment))
            throw new InvalidOperationException("Companion agent base endpoint must not contain a query or fragment.");

        return baseAddress;
    }
}
