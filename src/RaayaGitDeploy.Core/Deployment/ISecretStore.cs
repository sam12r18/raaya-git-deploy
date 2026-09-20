namespace RaayaGitDeploy.Core.Deployment;

/// <summary>
/// Stores deployment credentials outside project/profile persistence.
/// Implementations must use an OS-backed or equivalently protected secret store;
/// plaintext project files and application logs are not valid implementations.
/// </summary>
public interface ISecretStore
{
    Task SetAsync(SecretReference reference, string secret, CancellationToken cancellationToken);
    Task<string?> GetAsync(SecretReference reference, CancellationToken cancellationToken);
    Task DeleteAsync(SecretReference reference, CancellationToken cancellationToken);
}
