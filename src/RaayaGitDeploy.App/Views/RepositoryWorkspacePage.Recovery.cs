using Microsoft.UI.Xaml;
using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.App.Views;

public sealed partial class RepositoryWorkspacePage
{
    private void HistoryPrepareRetry_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (HistoryList.SelectedItem is not DeploymentHistoryEntry entry)
                throw new InvalidOperationException("Select a failed deployment first.");

            _deployment.PrepareRetry(entry);
            RefreshDeploymentSurface();
            HistoryRecoveryText.Text = $"Retry prepared for {_deployment.Queue.Items.Count} failed/blocked upload(s). Open Deploy Queue, review the server and run a new Dry Run before deploying.";
            ViewModel.SelectedSection = WorkspaceSection.DeployQueue;
            WorkbenchNavigation.SelectedItem = WorkbenchNavigation.MenuItems[4];
            UpdateSectionSurface();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }
}
