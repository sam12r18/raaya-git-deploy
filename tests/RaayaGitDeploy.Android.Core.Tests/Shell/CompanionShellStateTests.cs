using RaayaGitDeploy.Android.Core.Deployment;
using RaayaGitDeploy.Android.Core.Shell;
using RaayaGitDeploy.Core.Companion;

namespace RaayaGitDeploy.Android.Core.Tests.Shell;

public sealed class CompanionShellStateTests
{
    [Fact]
    public async Task Shell_OnlyUnlocksDeploymentAfterAuthorizedRepositoryAndProfileSelection()
    {
        var workflow = new CompanionDeploymentWorkflow(new FakeApi());
        var shell = new CompanionShellState(workflow);
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(CompanionShellScreen.Repositories, shell.Screen);
        Assert.False(shell.CanOpenDeployment);

        await workflow.LoadRepositoriesAsync(cancellationToken);
        await workflow.SelectRepositoryAsync("repo-1", cancellationToken);
        shell.OpenProfiles();
        Assert.Equal(CompanionShellScreen.Profiles, shell.Screen);
        Assert.False(shell.CanOpenDeployment);

        workflow.SelectProfile("prod");
        shell.OpenDeployment();
        Assert.Equal(CompanionShellScreen.Deployment, shell.Screen);
        Assert.True(shell.CanOpenDeployment);
    }

    private sealed class FakeApi : ICompanionDeploymentApi
    {
        public Task<IReadOnlyList<CompanionRepository>> GetRepositoriesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionRepository>>([new("repo-1", "Repo", "main", "abc123", true)]);

        public Task<IReadOnlyList<CompanionDeploymentProfile>> GetProfilesAsync(string repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionDeploymentProfile>>([new("prod", "Production", "production", true)]);

        public Task<CompanionDeploymentPreview> DryRunAsync(CompanionDeploymentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanionDeploymentRun> StartDeploymentAsync(string previewId, bool confirmed, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanionDeploymentRun> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
