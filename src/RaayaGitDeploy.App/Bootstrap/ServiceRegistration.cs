using Microsoft.Extensions.DependencyInjection;
using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Infrastructure.GitCli;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.App.Bootstrap;

public static class ServiceRegistration
{
    public static IServiceCollection AddRaayaGitDeployServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IGitProcessRunner, GitProcessRunner>();
        services.AddSingleton<IGitRepositoryService, GitRepositoryService>();
        services.AddTransient<RepositoryWorkspaceViewModel>();

        return services;
    }
}
