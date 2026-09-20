namespace RaayaGitDeploy.Core.Deployment;

/// <summary>
/// Opaque identifier for a credential held by a protected secret-store provider.
/// The value is safe to persist with a project/profile because it is never the secret itself.
/// </summary>
public readonly record struct SecretReference
{
    private const string Prefix = "secret://";

    private SecretReference(string value) => Value = value;

    public string Value { get; }

    public static SecretReference Create(string scope, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var normalizedScope = NormalizeSegment(scope, nameof(scope));
        var normalizedId = NormalizeSegment(id, nameof(id));
        return new SecretReference($"{Prefix}{normalizedScope}/{normalizedId}");
    }

    public static bool TryParse(string? value, out SecretReference reference)
    {
        reference = default;
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var payload = value[Prefix.Length..];
        var separator = payload.IndexOf('/');
        if (separator <= 0 || separator == payload.Length - 1 || payload.IndexOf('/', separator + 1) >= 0)
            return false;

        try
        {
            reference = Create(payload[..separator], payload[(separator + 1)..]);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public override string ToString() => Value ?? string.Empty;

    private static string NormalizeSegment(string value, string parameterName)
    {
        var normalized = value.Trim();
        if (normalized.Length > 128 || normalized.Any(character =>
                !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            throw new ArgumentException("Secret reference segments may contain only letters, digits, '.', '-' and '_'.", parameterName);
        }

        return normalized;
    }
}
