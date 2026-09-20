using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.App.Controls;

public sealed partial class RepositoryBranchSelector : UserControl
{
    public RepositoryBranchSelector()
    {
        InitializeComponent();
    }

    public RepositoryBranchSelectorViewModel? ViewModel { get; set; }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            ShowError("Repository tracking is not available.");
            return;
        }

        ViewModel.RepositoryPath = RepositoryPathBox.Text;
        ViewModel.RemoteName = RemoteNameBox.Text;
        ViewModel.BranchName = BranchNameBox.Text;

        SetBusy(true);
        StatusBar.IsOpen = false;
        RevisionPanel.Visibility = Visibility.Collapsed;

        try
        {
            await ViewModel.RefreshAsync(CancellationToken.None);
            RenderState();
        }
        catch (OperationCanceledException)
        {
            StatusBar.Severity = InfoBarSeverity.Informational;
            StatusBar.Title = "Fetch cancelled";
            StatusBar.Message = "No deployment source was changed.";
            StatusBar.IsOpen = true;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RenderState()
    {
        if (ViewModel is null)
        {
            return;
        }

        if (ViewModel.State == RepositoryTrackingState.Error)
        {
            ShowError(ViewModel.ErrorMessage ?? "Could not inspect the repository.");
            return;
        }

        if (ViewModel.State != RepositoryTrackingState.Success)
        {
            return;
        }

        LocalRevisionText.Text = ShortRevision(ViewModel.LocalRevision);
        RemoteRevisionText.Text = ShortRevision(ViewModel.RemoteRevision);
        RevisionPanel.Visibility = Visibility.Visible;

        StatusBar.Severity = ViewModel.HasRevisionDifference
            ? InfoBarSeverity.Warning
            : InfoBarSeverity.Success;
        StatusBar.Title = ViewModel.HasRevisionDifference
            ? "Remote changes available"
            : "Repository is in sync";
        StatusBar.Message = ViewModel.HasRevisionDifference
            ? "Review the remote revision in a deployment plan before transferring files."
            : "The local checkout and selected remote branch resolve to the same revision.";
        StatusBar.IsOpen = true;
    }

    private void SetBusy(bool isBusy)
    {
        Progress.IsActive = isBusy;
        Progress.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = !isBusy;
        RepositoryPathBox.IsEnabled = !isBusy;
        RemoteNameBox.IsEnabled = !isBusy;
        BranchNameBox.IsEnabled = !isBusy;
    }

    private void ShowError(string message)
    {
        StatusBar.Severity = InfoBarSeverity.Error;
        StatusBar.Title = "Repository check failed";
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }

    private static string ShortRevision(string revision) =>
        string.IsNullOrWhiteSpace(revision)
            ? "—"
            : revision[..Math.Min(12, revision.Length)];
}
