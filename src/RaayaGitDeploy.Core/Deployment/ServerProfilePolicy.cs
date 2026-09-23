namespace RaayaGitDeploy.Core.Deployment;

/// <summary>
/// Keeps transport-specific defaults and profile validation at the server-profile boundary.
/// Deployment planning/history must remain transport-neutral.
/// </summary>
public static class ServerProfilePolicy
{
    public static int DefaultPort(ServerTransportKind transport) => transport switch
    {
        ServerTransportKind.Sftp => 22,
        ServerTransportKind.Ftp => 21,
        ServerTransportKind.Ftps => 21,
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    public static ServerProfile Create(
        string id,
        string displayName,
        string host,
        int? port,
        string username,
        string remoteRoot,
        string credentialReference,
        ServerTransportKind transport)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Profile id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Profile name is required.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required.", nameof(username));
        if (string.IsNullOrWhiteSpace(remoteRoot)) throw new ArgumentException("Remote root is required.", nameof(remoteRoot));
        if (string.IsNullOrWhiteSpace(credentialReference)) throw new ArgumentException("Credential reference is required.", nameof(credentialReference));

        var resolvedPort = port ?? DefaultPort(transport);
        if (resolvedPort is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

        return new ServerProfile(
            id.Trim(),
            displayName.Trim(),
            host.Trim(),
            resolvedPort,
            username.Trim(),
            remoteRoot.Trim(),
            ServerAuthenticationMode.SshKey,
            credentialReference.Trim(),
            transport);
    }
}
