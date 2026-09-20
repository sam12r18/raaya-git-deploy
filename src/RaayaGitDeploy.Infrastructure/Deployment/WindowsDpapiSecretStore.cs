using System.Security.Cryptography;
using System.Text;
using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Infrastructure.Deployment;

/// <summary>
/// Stores opaque deployment secrets as per-user DPAPI protected blobs.
/// Only SecretReference values are safe to persist in project/profile configuration.
/// </summary>
public sealed class WindowsDpapiSecretStore : ISecretStore
{
    private readonly string _rootDirectory;

    public WindowsDpapiSecretStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = Path.GetFullPath(rootDirectory);
    }

    public async Task SetAsync(SecretReference reference, string secret, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        EnsureWindows();

        var plaintext = Encoding.UTF8.GetBytes(secret);
        try
        {
            var protectedBytes = ProtectedData.Protect(plaintext, GetEntropy(reference), DataProtectionScope.CurrentUser);
            var path = GetPath(reference);
            Directory.CreateDirectory(_rootDirectory);
            await File.WriteAllBytesAsync(path, protectedBytes, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public async Task<string?> GetAsync(SecretReference reference, CancellationToken cancellationToken)
    {
        EnsureWindows();
        var path = GetPath(reference);
        if (!File.Exists(path))
        {
            return null;
        }

        var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var plaintext = ProtectedData.Unprotect(protectedBytes, GetEntropy(reference), DataProtectionScope.CurrentUser);
        try
        {
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public Task DeleteAsync(SecretReference reference, CancellationToken cancellationToken)
    {
        EnsureWindows();
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(reference);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetPath(SecretReference reference)
    {
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reference.Value))).ToLowerInvariant();
        return Path.Combine(_rootDirectory, $"{id}.secret");
    }

    private static byte[] GetEntropy(SecretReference reference) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"RaayaGitDeploy|{reference.Value}"));

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The DPAPI secret store is available only on Windows.");
        }
    }
}
