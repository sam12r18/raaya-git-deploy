using Microsoft.Extensions.DependencyInjection;
using RaayaGitDeploy.Core.Commands;
using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Core.Terminal;
using RaayaGitDeploy.Infrastructure.Commands;
using RaayaGitDeploy.Infrastructure.Deployment;
using RaayaGitDeploy.Infrastructure.GitCli;
using RaayaGitDeploy.Infrastructure.Terminal;
using RaayaGitDeploy.Presentation.Commands;
using RaayaGitDeploy.Presentation.Deployment;
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

        services.AddSingleton<IServerProfileStore>(_ => new JsonServerProfileStore(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RaayaGitDeploy", "servers.json")));
        services.AddSingleton<IRemoteTransport, UnavailableRemoteTransport>();
        services.AddTransient<DeploymentQueueViewModel>();
        services.AddTransient<ServersViewModel>();
        services.AddTransient<DeploymentPlanner>();
        services.AddTransient<DeploymentDryRunViewModel>();
        services.AddTransient<DeploymentWorkspaceViewModel>();
        services.AddTransient<RepositoryWorkspaceViewModel>();

        return services;
    }

    private sealed class UnavailableRemoteTransport : IRemoteTransport
    {
        private static NotSupportedException Error() => new("Real SFTP connection is not wired yet. Profiles and Dry Run are available; remote operations remain disabled.");
        public Task TestConnectionAsync(ServerProfile profile, CancellationToken cancellationToken) => Task.FromException(Error());
        public Task<IReadOnlyList<string>> ListAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) => Task.FromException<IReadOnlyList<string>>(Error());
        public Task UploadAsync(ServerProfile profile, string localPath, string remotePath, CancellationToken cancellationToken) => Task.FromException(Error());
        public Task DeleteAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) => Task.FromException(Error());
    }
}
