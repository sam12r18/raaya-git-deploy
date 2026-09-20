using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Workspace;

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
            HistoryRecoveryText.Text = $"Retry prepared for {_deployment.Queue.Items.Count} failed/blocked upload(s). Review the server and run a new Dry Run before deploying.";
            NavigateToSection(WorkspaceSection.DeployQueue);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void NavigateToSection(WorkspaceSection section)
    {
        var tag = section.ToString();
        var item = WorkbenchNavigation.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(candidate => string.Equals(candidate.Tag?.ToString(), tag, StringComparison.Ordinal));

        if (item is null)
            throw new InvalidOperationException($"Navigation item for {section} is not available.");

        ViewModel.SelectedSection = section;
        WorkbenchNavigation.SelectedItem = item;
        UpdateSectionSurface();
    }
}
