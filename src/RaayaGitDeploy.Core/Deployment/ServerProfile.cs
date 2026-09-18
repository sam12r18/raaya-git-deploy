namespace RaayaGitDeploy.Core.Deployment;

public enum ServerAuthenticationMode
{
    SshKey
}

public sealed record ServerProfile(
    string Id,
    string DisplayName,
    string Host,
    int Port,
    string Username,
    string RemoteRoot,
    ServerAuthenticationMode AuthenticationMode,
    string KeyReference);
