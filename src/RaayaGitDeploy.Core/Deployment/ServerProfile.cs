namespace RaayaGitDeploy.Core.Deployment;

/// <summary>
/// Selects the remote transport without exposing transport-specific connection details to deployment planning/history.
/// </summary>
public enum ServerTransportKind
{
    Sftp,
    Ftp,
    Ftps
}

public enum ServerAuthenticationMode
{
    SshKey,
    ExternalCredentialReference
}

public sealed record ServerProfile(
    string Id,
    string DisplayName,
    string Host,
    int Port,
    string Username,
    string RemoteRoot,
    ServerAuthenticationMode AuthenticationMode,
    string KeyReference,
    ServerTransportKind Transport = ServerTransportKind.Sftp);
