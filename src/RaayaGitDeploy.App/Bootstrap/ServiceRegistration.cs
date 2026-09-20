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
        services.AddSingleton<IGitRemoteTrackingService, GitRemoteTrackingService>();
        services.AddTransient<ITerminalProcessAdapter, PowerShellProcessAdapter>();
        services.AddTransient<ITerminalSessionFactory>(provider =>
            new ConPtyTerminalSessionFactory(() => provider.GetRequiredService<ITerminalProcessAdapter>()));
        services.AddTransient<TerminalViewModel>();
        services.AddSingleton<ISavedCommandStore>(_ => new JsonSavedCommandStore(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RaayaGitDeploy", "commands.json")));
        services.AddTransient<ICommandExecutionService, TerminalCommandExecutionService>();
        services.AddTransient<CommandsViewModel>();

        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RaayaGitDeploy");
        services.AddSingleton<IServerProfileStore>(_ => new JsonServerProfileStore(Path.Combine(appData, "servers.json")));
        services.AddSingleton<ISecretStore>(_ => new WindowsDpapiSecretStore(Path.Combine(appData, "secrets")));
        services.AddSingleton<IHostKeyVerifier>(_ => new TofuHostKeyVerifier(Path.Combine(appData, "known-hosts")));
        services.AddTransient<ISftpClientAdapter>(_ => throw new InvalidOperationException("Resolve SFTP adapters through the profile factory."));
        services.AddSingleton<IRemoteTransport>(provider => new SftpRemoteTransport(
            profile => new SshNetSftpClientAdapter(profile, provider.GetRequiredService<ISecretStore>()),
            provider.GetRequiredService<IHostKeyVerifier>()));
        services.AddTransient<DeploymentQueueViewModel>();
        services.AddTransient<ServersViewModel>();
        services.AddTransient<HostProfileEditorViewModel>();
        services.AddTransient<DeploymentPlanner>();
        services.AddTransient<DeploymentDryRunViewModel>();
        services.AddTransient<DryRunSummaryViewModel>();
        services.AddTransient<DeploymentWorkspaceViewModel>();
        services.AddTransient<RepositoryWorkspaceViewModel>();

        return services;
    }
}
