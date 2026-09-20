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

    public DeploymentProjectEditorViewModel? ViewModel { get; set; }

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

    public void LoadFromViewModel()
    {
        if (ViewModel is null)
        {
            return;
        }

        ProjectName.Text = ViewModel.DisplayName;
        RepositoryRoot.Text = ViewModel.RepositoryRoot;
        RemoteName.Text = ViewModel.RemoteName;
        BranchName.Text = ViewModel.Branch;
        ApplicationRoot.Text = ViewModel.ApplicationRoot;
        PublicRoot.Text = ViewModel.PublicRoot;
        ProtectedPaths.Text = ViewModel.ProtectedPathsText;
        Strategy.SelectedIndex = ViewModel.Strategy == DeploymentStrategy.Laravel ? 1 : 0;

        ServerProfile.SelectedItem = _hostProfiles.FirstOrDefault(profile => profile.Id == ViewModel.ServerProfileId)
            ?? _hostProfiles.FirstOrDefault();
        UpdateHostProfileState();
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
        if (ViewModel is null)
        {
            ValidationInfo.Message = "Deployment project state is not available.";
            ValidationInfo.IsOpen = true;
            return;
        }

        if (ServerProfile.SelectedItem is not ServerProfile selectedProfile)
        {
            ValidationInfo.Message = "Select a saved host profile before validating the project.";
            ValidationInfo.IsOpen = true;
            return;
        }

        ViewModel.DisplayName = ProjectName.Text;
        ViewModel.RepositoryRoot = RepositoryRoot.Text;
        ViewModel.RemoteName = RemoteName.Text;
        ViewModel.Branch = BranchName.Text;
        ViewModel.ServerProfileId = selectedProfile.Id;
        ViewModel.ApplicationRoot = ApplicationRoot.Text;
        ViewModel.PublicRoot = PublicRoot.Text;
        ViewModel.Strategy = Strategy.SelectedIndex == 1 ? DeploymentStrategy.Laravel : DeploymentStrategy.FileSync;
        ViewModel.ProtectedPathsText = ProtectedPaths.Text;

        if (!ViewModel.TryCreate(out var project) || project is null)
        {
            ValidationInfo.Message = ViewModel.ValidationError ?? "Project configuration is invalid.";
            ValidationInfo.IsOpen = true;
            return;
        }

        ValidationInfo.IsOpen = false;
        ProjectValidated?.Invoke(this, project);
    }
}
