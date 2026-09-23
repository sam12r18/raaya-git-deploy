namespace RaayaGitDeploy.Android.Core.Security;

/// <summary>
/// Platform cryptography boundary for companion session material.
/// The Android implementation must keep key material in Android Keystore and must never
/// expose raw SSH, FTP, Git, or deployment credentials through this contract.
/// </summary>
public interface IPlatformAccessTokenProtector
{
    Task<byte[]> ProtectAsync(string accessToken, CancellationToken cancellationToken);
    Task<string?> UnprotectAsync(byte[] protectedToken, CancellationToken cancellationToken);
}

/// <summary>
/// Persistence boundary for already-protected companion session bytes. Implementations may
/// use app-private preferences/files, but the bytes supplied here must already be encrypted.
/// </summary>
public interface IProtectedAccessTokenPersistence
{
    Task<byte[]?> ReadAsync(CancellationToken cancellationToken);
    Task WriteAsync(byte[] protectedToken, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
