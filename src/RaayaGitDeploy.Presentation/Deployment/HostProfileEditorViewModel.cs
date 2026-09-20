using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Presentation.Deployment;

public enum ConnectionValidationState
{
    Idle,
    Validating,
    Success,
    Error
}

public sealed class HostProfileEditorViewModel
{
    private readonly ServersViewModel _servers;

    public HostProfileEditorViewModel(ServersViewModel servers)
    {
        _servers = servers ?? throw new ArgumentNullException(nameof(servers));
    }

    public string DisplayName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string RemoteRoot { get; set; } = string.Empty;
    public string KeyReference { get; set; } = string.Empty;
    public ConnectionValidationState ConnectionState { get; private set; } = ConnectionValidationState.Idle;
    public string? StatusMessage { get; private set; }
    public bool IsBusy => ConnectionState == ConnectionValidationState.Validating;

    public ServerProfile CreateProfile(string? id = null)
    {
        if (string.IsNullOrWhiteSpace(DisplayName)) throw new ArgumentException("Host profile name is required.");
        if (string.IsNullOrWhiteSpace(Host)) throw new ArgumentException("Host is required.");
        if (Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(Username)) throw new ArgumentException("Username is required.");
        if (string.IsNullOrWhiteSpace(RemoteRoot)) throw new ArgumentException("Remote root is required.");
        if (string.IsNullOrWhiteSpace(KeyReference)) throw new ArgumentException("SSH key reference is required. Raw private keys must not be stored in the profile.");
        if (!SecretReference.TryParse(KeyReference, out var secretReference))
            throw new ArgumentException("SSH credentials must use a protected secret:// reference. Legacy private-key file paths must be imported into the protected credential store first.");

        return new ServerProfile(
            id ?? Guid.NewGuid().ToString("N"),
            DisplayName.Trim(),
            Host.Trim(),
            Port,
            Username.Trim(),
            RemoteRoot.Trim(),
            ServerAuthenticationMode.SshKey,
            secretReference.ToString());
    }

    public async Task<ServerProfile?> SaveAndValidateAsync(string? id, CancellationToken cancellationToken)
    {
        ServerProfile profile;
        try
        {
            profile = CreateProfile(id);
        }
        catch (ArgumentException exception)
        {
            ConnectionState = ConnectionValidationState.Error;
            StatusMessage = exception.Message;
            return null;
        }

        ConnectionState = ConnectionValidationState.Validating;
        StatusMessage = "Testing secure connection…";

        try
        {
            await _servers.SaveAsync(profile, cancellationToken);
            _servers.SelectedProfile = profile;
            await _servers.TestSelectedConnectionAsync(cancellationToken);
            ConnectionState = ConnectionValidationState.Success;
            StatusMessage = "Connection validated.";
            return profile;
        }
        catch (OperationCanceledException)
        {
            ConnectionState = ConnectionValidationState.Idle;
            StatusMessage = "Connection test cancelled.";
            throw;
        }
        catch (Exception exception)
        {
            ConnectionState = ConnectionValidationState.Error;
            StatusMessage = $"Connection failed: {exception.Message}";
            return null;
        }
    }
}
