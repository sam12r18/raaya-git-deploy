using Microsoft.UI.Xaml;
using RaayaGitDeploy.App.Services;
using RaayaGitDeploy.App.Views;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.App;

public sealed partial class MainWindow : Window
{
    public MainWindow(RepositoryWorkspaceViewModel viewModel)
    {
        InitializeComponent();

        var folderPicker = new WindowsRepositoryFolderPicker(this);
        var openCoordinator = new RepositoryOpenCoordinator(folderPicker, viewModel);
        RootHost.Children.Add(new RepositoryWorkspacePage(viewModel, openCoordinator));
    }
}
