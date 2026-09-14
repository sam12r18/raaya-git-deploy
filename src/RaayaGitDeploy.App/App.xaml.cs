using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using RaayaGitDeploy.App.Bootstrap;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        Services = new ServiceCollection()
            .AddRaayaGitDeployServices()
            .BuildServiceProvider();
    }

    public IServiceProvider Services { get; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var viewModel = Services.GetRequiredService<RepositoryWorkspaceViewModel>();
        _window = new MainWindow(viewModel);
        _window.Activate();
    }
}
