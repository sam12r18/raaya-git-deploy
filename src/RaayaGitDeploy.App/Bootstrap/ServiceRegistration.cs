using Microsoft.Extensions.DependencyInjection;
using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Core.Terminal;
using RaayaGitDeploy.Infrastructure.GitCli;
using RaayaGitDeploy.Infrastructure.Terminal;
using RaayaGitDeploy.Presentation.Terminal;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.App.Bootstrap;

public static class ServiceRegistration
{
    public static IServiceCollection AddRaayaGitDeployServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IGitProcessRunner, GitProcessRunner>();
        services.AddSingleton<IGitRepositoryService, GitRepositoryService>();
        services.AddTransient<ITerminalProcessAdapter, PowerShellProcessAdapter>();
        services.AddTransient<ITerminalSessionFactory>(provider =>
            new ConPtyTerminalSessionFactory(() => provider.GetRequiredService<ITerminalProcessAdapter>()));
        services.AddTransient<TerminalViewModel>();
        services.AddTransient<RepositoryWorkspaceViewModel>();

        return services;
    }
}
