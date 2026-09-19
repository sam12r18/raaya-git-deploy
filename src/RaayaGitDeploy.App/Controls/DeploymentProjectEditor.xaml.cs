using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.App.Controls;

public sealed partial class DeploymentProjectEditor : UserControl
{
    public DeploymentProjectEditor()
    {
        InitializeComponent();
    }

    public event EventHandler<DeploymentProject>? ProjectValidated;

    private void ValidateProject_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = new DeploymentProjectEditorViewModel
        {
            DisplayName = ProjectName.Text,
            RepositoryRoot = RepositoryRoot.Text,
            RemoteName = RemoteName.Text,
            Branch = BranchName.Text,
            ServerProfileId = ServerProfileId.Text,
            ApplicationRoot = ApplicationRoot.Text,
            PublicRoot = PublicRoot.Text,
            Strategy = Strategy.SelectedIndex == 1 ? DeploymentStrategy.Laravel : DeploymentStrategy.FileSync,
            ProtectedPathsText = ProtectedPaths.Text
        };

        if (!viewModel.TryCreate(out var project) || project is null)
        {
            ValidationInfo.Message = viewModel.ValidationError ?? "Project configuration is invalid.";
            ValidationInfo.IsOpen = true;
            return;
        }

        ValidationInfo.IsOpen = false;
        ProjectValidated?.Invoke(this, project);
    }
}
