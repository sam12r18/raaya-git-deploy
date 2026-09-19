using Microsoft.Extensions.DependencyInjection;
using RaayaGitDeploy.Core.Commands;
using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Core.Terminal;
using RaayaGitDeploy.Infrastructure.Commands;
using RaayaGitDeploy.Infrastructure.GitCli;
using RaayaGitDeploy.Infrastructure.Terminal;
using RaayaGitDeploy.Presentation.Commands;
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
        services.AddSingleton<ISavedCommandStore>(_ => new JsonSavedCommandStore(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RaayaGitDeploy", "commands.json")));
        services.AddTransient<ICommandExecutionService, TerminalCommandExecutionService>();
        services.AddTransient<CommandsViewModel>();
        services.AddTransient<RepositoryWorkspaceViewModel>();

        return services;
    }
}
