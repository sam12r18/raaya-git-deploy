using Microsoft.UI.Xaml;
using RaayaGitDeploy.App.Views;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.App;

public sealed partial class MainWindow : Window
{
    public MainWindow(RepositoryWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        RootHost.Children.Add(new RepositoryWorkspacePage(this, viewModel));
    }
}
