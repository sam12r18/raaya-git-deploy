using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.App.Controls;

public sealed partial class DryRunSummary : UserControl
{
    public DryRunSummary()
    {
        InitializeComponent();
    }

    public DryRunSummaryViewModel? ViewModel { get; set; }

    public void Render()
    {
        if (ViewModel is null)
        {
            ShowError("Dry-run planning is not available.");
            return;
        }

        if (!ViewModel.IsReady)
        {
            SummaryPanel.Visibility = Visibility.Collapsed;
            OperationList.ItemsSource = null;
            ShowError(ViewModel.ErrorMessage ?? "Build a deployment plan to preview file operations.");
            return;
        }

        UploadCountText.Text = ViewModel.UploadCount.ToString();
        DeleteCountText.Text = ViewModel.DeleteCount.ToString();
        ProtectedCountText.Text = ViewModel.ProtectedSkipCount.ToString();
        OperationList.ItemsSource = ViewModel.Operations;
        SummaryPanel.Visibility = Visibility.Visible;

        StateBar.Severity = ViewModel.HasDestructiveOperations ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
        StateBar.Title = ViewModel.HasDestructiveOperations ? "Destructive operations detected" : "Dry run ready";
        StateBar.Message = ViewModel.HasDestructiveOperations
            ? "This preview contains delete operations. No files have been changed; explicit confirmation is required before a real deployment."
            : "No destructive operation is present in this preview. No files have been transferred.";
        StateBar.IsOpen = true;
    }

    private void ShowError(string message)
    {
        StateBar.Severity = InfoBarSeverity.Error;
        StateBar.Title = "Dry run unavailable";
        StateBar.Message = message;
        StateBar.IsOpen = true;
    }
}
