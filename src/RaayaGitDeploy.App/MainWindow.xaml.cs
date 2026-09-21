using Microsoft.UI.Xaml;
using RaayaGitDeploy.App.Services;
using RaayaGitDeploy.App.Views;
using RaayaGitDeploy.Presentation.Commands;
using RaayaGitDeploy.Presentation.Deployment;
using RaayaGitDeploy.Presentation.Terminal;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.App;

public sealed partial class MainWindow : Window
{
    public MainWindow(RepositoryWorkspaceViewModel viewModel, TerminalViewModel terminalViewModel, CommandsViewModel commandsViewModel, DeploymentWorkspaceViewModel deploymentViewModel)
    {
        InitializeComponent();
        var folderPicker = new WindowsRepositoryFolderPicker(this);
        // The coordinator must own the live deployment workspace as well as the Git workspace. Without
        // this wiring the real Windows shell can switch repositories during a deployment and cannot
        // clear queue/dry-run state after a successful repository change, even though the coordinator
        // correctly enforces both invariants in isolation.
        var openCoordinator = new RepositoryOpenCoordinator(folderPicker, viewModel, deploymentViewModel);
        RootHost.Children.Add(new RepositoryWorkspacePage(viewModel, terminalViewModel, commandsViewModel, deploymentViewModel, openCoordinator));
    }
}
