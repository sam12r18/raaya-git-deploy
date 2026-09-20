using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Deployment;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace RaayaGitDeploy.App.Controls;

public sealed partial class HostProfileCard : UserControl
{
    public HostProfileCard()
    {
        InitializeComponent();
    }

    public HostProfileEditorViewModel? ViewModel { get; set; }
    public string? ProfileId { get; set; }

    public event EventHandler<ServerProfile>? ProfileValidated;

    public void LoadProfile(ServerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ProfileId = profile.Id;
        DisplayName.Text = profile.DisplayName;
        Host.Text = profile.Host;
        Port.Value = profile.Port;
        Username.Text = profile.Username;
        RemoteRoot.Text = profile.RemoteRoot;
        KeyReference.Text = profile.KeyReference;
        UpdateCredentialState();
        ConnectionInfo.IsOpen = false;
    }

    public void ClearProfile()
    {
        ProfileId = null;
        DisplayName.Text = string.Empty;
        Host.Text = string.Empty;
        Port.Value = 22;
        Username.Text = string.Empty;
        RemoteRoot.Text = string.Empty;
        KeyReference.Text = string.Empty;
        UpdateCredentialState();
        ConnectionInfo.IsOpen = false;
    }

    private async void ImportPrivateKey_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            ShowStatus(ConnectionValidationState.Error, "Host credential service is not available.");
            return;
        }

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");

        var window = App.MainWindow;
        if (window is null)
        {
            ShowStatus(ConnectionValidationState.Error, "Unable to open the private-key picker.");
            return;
        }

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var privateKey = await Windows.Storage.FileIO.ReadTextAsync(file);
            var referenceName = string.IsNullOrWhiteSpace(DisplayName.Text)
                ? Path.GetFileNameWithoutExtension(file.Name)
                : DisplayName.Text;
            var reference = await ViewModel.ImportPrivateKeyAsync(privateKey, referenceName, CancellationToken.None);
            KeyReference.Text = reference.ToString();
            UpdateCredentialState();
            ShowStatus(ConnectionValidationState.Idle, "Private key imported into protected local storage.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ShowStatus(ConnectionValidationState.Error, ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            ShowStatus(ConnectionValidationState.Error, "Host validation service is not available.");
            return;
        }

        ViewModel.DisplayName = DisplayName.Text;
        ViewModel.Host = Host.Text;
        ViewModel.Port = double.IsNaN(Port.Value) ? 0 : (int)Port.Value;
        ViewModel.Username = Username.Text;
        ViewModel.RemoteRoot = RemoteRoot.Text;
        ViewModel.KeyReference = KeyReference.Text;

        SetBusy(true);
        ShowStatus(ConnectionValidationState.Validating, "Testing secure connection…");

        try
        {
            var profile = await ViewModel.SaveAndValidateAsync(ProfileId, CancellationToken.None);
            ShowStatus(ViewModel.ConnectionState, ViewModel.StatusMessage);

            if (profile is not null && ViewModel.ConnectionState == ConnectionValidationState.Success)
            {
                ProfileId = profile.Id;
                ProfileValidated?.Invoke(this, profile);
            }
        }
        catch (OperationCanceledException)
        {
            ShowStatus(ConnectionValidationState.Idle, "Connection test cancelled.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void UpdateCredentialState()
    {
        var hasCredential = !string.IsNullOrWhiteSpace(KeyReference.Text);
        CredentialStateText.Text = hasCredential ? "Protected key ready" : "No protected key imported";
        KeyReference.Visibility = hasCredential ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetBusy(bool isBusy)
    {
        TestConnectionButton.IsEnabled = !isBusy;
        ImportPrivateKeyButton.IsEnabled = !isBusy;
        DisplayName.IsEnabled = !isBusy;
        Host.IsEnabled = !isBusy;
        Port.IsEnabled = !isBusy;
        Username.IsEnabled = !isBusy;
        RemoteRoot.IsEnabled = !isBusy;
        ConnectionProgress.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowStatus(ConnectionValidationState state, string? message)
    {
        ConnectionInfo.Message = message ?? string.Empty;
        ConnectionInfo.Severity = state switch
        {
            ConnectionValidationState.Success => InfoBarSeverity.Success,
            ConnectionValidationState.Error => InfoBarSeverity.Error,
            ConnectionValidationState.Validating => InfoBarSeverity.Informational,
            _ => InfoBarSeverity.Informational
        };
        ConnectionInfo.IsOpen = !string.IsNullOrWhiteSpace(message);
    }
}
