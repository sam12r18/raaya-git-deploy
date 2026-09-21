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

    [Fact]
    public async Task Shell_OnlyOpensHistoryDetailForLoadedRepositoryHistory()
    {
        var workflow = new CompanionDeploymentWorkflow(new FakeApi());
        var shell = new CompanionShellState(workflow);
        var cancellationToken = TestContext.Current.CancellationToken;

        await workflow.LoadRepositoriesAsync(cancellationToken);
        await workflow.SelectRepositoryAsync("repo-1", cancellationToken);
        await workflow.LoadHistoryAsync(cancellationToken);

        shell.OpenHistory();
        shell.OpenHistoryDetail("deploy-1");

        Assert.Equal(CompanionShellScreen.HistoryDetail, shell.Screen);
        Assert.Throws<InvalidOperationException>(() => shell.OpenHistoryDetail("foreign-deploy"));
    }

    private sealed class FakeApi : ICompanionDeploymentApi
    {
        public Task<IReadOnlyList<CompanionRepository>> GetRepositoriesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionRepository>>([new("repo-1", "Repo", "main", "abc123", true)]);

        public Task<IReadOnlyList<CompanionDeploymentProfile>> GetProfilesAsync(string repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionDeploymentProfile>>([new("prod", "Production", "production", true)]);

        public Task<IReadOnlyList<CompanionDeploymentRun>> GetDeploymentHistoryAsync(string repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionDeploymentRun>>([
                new("deploy-1", repositoryId, "prod", "succeeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null)
            ]);

        public Task<CompanionDeploymentPreview> DryRunAsync(CompanionDeploymentRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanionDeploymentRun> StartDeploymentAsync(string previewId, bool confirmed, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CompanionDeploymentRun> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
