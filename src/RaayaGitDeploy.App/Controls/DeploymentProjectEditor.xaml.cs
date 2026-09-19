using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.App.Controls;

public sealed partial class DeploymentProjectEditor : UserControl
{
    private IReadOnlyList<ServerProfile> _hostProfiles = [];

    public DeploymentProjectEditor()
    {
        InitializeComponent();
    }

    public event EventHandler<DeploymentProject>? ProjectValidated;

    public IReadOnlyList<ServerProfile> HostProfiles
    {
        get => _hostProfiles;
        set
        {
            _hostProfiles = value ?? [];
            ServerProfile.ItemsSource = _hostProfiles;
            ServerProfile.SelectedItem = _hostProfiles.FirstOrDefault();
            UpdateHostProfileState();
        }
    }

    private void ServerProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateHostProfileState();
    }

    private void UpdateHostProfileState()
    {
        if (ServerProfile.SelectedItem is ServerProfile profile)
        {
            HostProfileInfo.Severity = InfoBarSeverity.Success;
            HostProfileInfo.Message = $"Target: {profile.Name} ({profile.Host}:{profile.Port}). Test the connection from Host Profiles before deployment.";
            return;
        }

        HostProfileInfo.Severity = InfoBarSeverity.Warning;
        HostProfileInfo.Message = _hostProfiles.Count == 0
            ? "No saved host profile is available. Create and validate one before deployment."
            : "Select a saved host profile before validating this project.";
    }

    private void ValidateProject_Click(object sender, RoutedEventArgs e)
    {
        if (ServerProfile.SelectedItem is not ServerProfile selectedProfile)
        {
            ValidationInfo.Message = "Select a saved host profile before validating the project.";
            ValidationInfo.IsOpen = true;
            return;
        }

        var viewModel = new DeploymentProjectEditorViewModel
        {
            DisplayName = ProjectName.Text,
            RepositoryRoot = RepositoryRoot.Text,
            RemoteName = RemoteName.Text,
            Branch = BranchName.Text,
            ServerProfileId = selectedProfile.Id,
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
