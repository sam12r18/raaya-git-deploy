using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Deployment;

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

    private void SetBusy(bool isBusy)
    {
        TestConnectionButton.IsEnabled = !isBusy;
        DisplayName.IsEnabled = !isBusy;
        Host.IsEnabled = !isBusy;
        Port.IsEnabled = !isBusy;
        Username.IsEnabled = !isBusy;
        RemoteRoot.IsEnabled = !isBusy;
        KeyReference.IsEnabled = !isBusy;
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
