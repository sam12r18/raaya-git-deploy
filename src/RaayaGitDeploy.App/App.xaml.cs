using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using RaayaGitDeploy.App.Bootstrap;
using RaayaGitDeploy.Presentation.Commands;
using RaayaGitDeploy.Presentation.Deployment;
using RaayaGitDeploy.Presentation.Terminal;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        Services = new ServiceCollection().AddRaayaGitDeployServices().BuildServiceProvider();
    }

    public IServiceProvider Services { get; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var viewModel = Services.GetRequiredService<RepositoryWorkspaceViewModel>();
        var terminalViewModel = Services.GetRequiredService<TerminalViewModel>();
        var commandsViewModel = Services.GetRequiredService<CommandsViewModel>();
        var deploymentViewModel = Services.GetRequiredService<DeploymentWorkspaceViewModel>();
        _window = new MainWindow(viewModel, terminalViewModel, commandsViewModel, deploymentViewModel);
        _window.Activate();
    }
}
