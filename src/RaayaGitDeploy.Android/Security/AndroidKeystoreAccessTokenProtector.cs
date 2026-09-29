using System.Text;
using Android.Content;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using RaayaGitDeploy.Android.Core.Security;

namespace RaayaGitDeploy.Android.Security;

/// <summary>
/// Protects the companion API/session token with an AES key that is generated and retained
/// by Android Keystore. The encrypted payload contains only IV + ciphertext/tag; key material
/// is never exported from the platform keystore.
/// </summary>
public sealed class AndroidKeystoreAccessTokenProtector : Java.Lang.Object, IPlatformAccessTokenProtector
{
    private const string KeyAlias = "raaya_git_deploy_companion_session_v1";
    private const string Transformation = "AES/GCM/NoPadding";
    private const int IvLength = 12;
    private const int TagLengthBits = 128;

    public Task<byte[]> ProtectAsync(string accessToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        cancellationToken.ThrowIfCancellationRequested();

        var key = GetOrCreateKey();
        using var cipher = Cipher.GetInstance(Transformation) ?? throw new CryptographicException("AES/GCM is unavailable on this Android device.");
        cipher.Init(CipherMode.EncryptMode, key);
        var iv = cipher.GetIV() ?? throw new CryptographicException("Android did not provide an AES/GCM IV.");
        var encrypted = cipher.DoFinal(Encoding.UTF8.GetBytes(accessToken)) ?? throw new CryptographicException("Android Keystore encryption returned no data.");

        var payload = new byte[1 + iv.Length + encrypted.Length];
        payload[0] = checked((byte)iv.Length);
        Buffer.BlockCopy(iv, 0, payload, 1, iv.Length);
        Buffer.BlockCopy(encrypted, 0, payload, 1 + iv.Length, encrypted.Length);
        return Task.FromResult(payload);
    }

    public Task<string?> UnprotectAsync(byte[] protectedToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(protectedToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (protectedToken.Length < 1 + IvLength + 16) throw new CryptographicException("Protected companion session payload is invalid.");

        var ivLength = protectedToken[0];
        if (ivLength != IvLength || protectedToken.Length <= 1 + ivLength) throw new CryptographicException("Protected companion session payload has an invalid IV.");

        var iv = protectedToken.AsSpan(1, ivLength).ToArray();
        var encrypted = protectedToken.AsSpan(1 + ivLength).ToArray();
        var key = GetExistingKey() ?? throw new CryptographicException("The Android Keystore session key is unavailable. Sign in again.");

        using var cipher = Cipher.GetInstance(Transformation) ?? throw new CryptographicException("AES/GCM is unavailable on this Android device.");
        using var parameters = new GCMParameterSpec(TagLengthBits, iv);
        cipher.Init(CipherMode.DecryptMode, key, parameters);
        var plaintext = cipher.DoFinal(encrypted) ?? throw new CryptographicException("Android Keystore decryption returned no data.");
        return Task.FromResult<string?>(Encoding.UTF8.GetString(plaintext));
    }

    private static IKey GetOrCreateKey()
    {
        var existing = GetExistingKey();
        if (existing is not null) return existing;

        using var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore")
            ?? throw new CryptographicException("Android Keystore AES key generator is unavailable.");
        using var spec = new KeyGenParameterSpec.Builder(KeyAlias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
            .SetRandomizedEncryptionRequired(true)
            .Build();
        generator.Init(spec);
        return generator.GenerateKey() ?? throw new CryptographicException("Android Keystore failed to generate the session key.");
    }

    private static IKey? GetExistingKey()
    {
        using var keyStore = KeyStore.GetInstance("AndroidKeyStore") ?? throw new CryptographicException("Android Keystore is unavailable.");
        keyStore.Load(null);
        return keyStore.GetKey(KeyAlias, null);
    }
}

/// <summary>
/// Persists only the already-encrypted Keystore payload in app-private SharedPreferences.
/// Raw tokens and transport credentials are never written here.
/// </summary>
public sealed class AndroidProtectedAccessTokenPersistence(Context context) : IProtectedAccessTokenPersistence
{
    private const string PreferenceName = "raaya_git_deploy_secure_session";
    private const string TokenKey = "protected_companion_token";
    private readonly Context _context = context.ApplicationContext ?? context;

    public Task<byte[]?> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var encoded = Preferences.GetString(TokenKey, null);
        return Task.FromResult(string.IsNullOrWhiteSpace(encoded) ? null : Convert.FromBase64String(encoded));
    }

    public Task WriteAsync(byte[] protectedToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(protectedToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (protectedToken.Length == 0) throw new ArgumentException("Protected token payload cannot be empty.", nameof(protectedToken));
        if (!Preferences.Edit()!.PutString(TokenKey, Convert.ToBase64String(protectedToken))!.Commit())
            throw new IOException("Could not persist the protected companion session.");
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Preferences.Edit()!.Remove(TokenKey)!.Commit())
            throw new IOException("Could not clear the protected companion session.");
        return Task.CompletedTask;
    }

    private ISharedPreferences Preferences => _context.GetSharedPreferences(PreferenceName, FileCreationMode.Private)
        ?? throw new InvalidOperationException("App-private Android preferences are unavailable.");
}
