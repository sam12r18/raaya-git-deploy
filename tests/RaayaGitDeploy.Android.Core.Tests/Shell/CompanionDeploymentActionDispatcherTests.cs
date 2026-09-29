using RaayaGitDeploy.Android.Core.Deployment;
using RaayaGitDeploy.Android.Core.Shell;
using RaayaGitDeploy.Core.Companion;

namespace RaayaGitDeploy.Android.Core.Tests.Shell;

public sealed class CompanionDeploymentActionDispatcherTests
{
    [Fact]
    public async Task Refresh_DispatchesOnlyWhenCurrentRunIsRefreshable()
    {
        var api = new DispatcherApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        var shell = new CompanionShellState(workflow);
        var dispatcher = new CompanionDeploymentActionDispatcher(shell);
        var cancellationToken = TestContext.Current.CancellationToken;

        await workflow.LoadRepositoriesAsync(cancellationToken);
        await workflow.SelectRepositoryAsync("repo-1", cancellationToken);
        workflow.SelectProfile("prod");
        shell.OpenDeployment();
        await shell.DryRunAsync(["src/app.cs"], cancellationToken);
        await shell.StartDeploymentAsync(confirmed: true, cancellationToken);

        var result = await dispatcher.DispatchAsync(CompanionDeploymentScreenAction.Refresh, cancellationToken);

        Assert.Equal(CompanionDeploymentActionResult.Completed, result);
        Assert.Equal(1, api.RefreshCount);
        Assert.Equal("succeeded", shell.CurrentDeploymentState);
    }

    [Fact]
    public async Task Retry_RequiresFreshConfirmationAndNeverStartsDeployment()
    {
        var api = new DispatcherApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        var shell = new CompanionShellState(workflow);
        var dispatcher = new CompanionDeploymentActionDispatcher(shell);

        var result = await dispatcher.DispatchAsync(
            CompanionDeploymentScreenAction.Retry,
            TestContext.Current.CancellationToken);

        Assert.Equal(CompanionDeploymentActionResult.RequiresDeploymentConfirmation, result);
        Assert.Equal(0, api.StartCount);
    }

    private sealed class DispatcherApi : ICompanionDeploymentApi
    {
        public int StartCount { get; private set; }
        public int RefreshCount { get; private set; }

        public Task<IReadOnlyList<CompanionRepository>> GetRepositoriesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionRepository>>([new("repo-1", "Repo", "main", "abc123", true)]);

        public Task<IReadOnlyList<CompanionDeploymentProfile>> GetProfilesAsync(string repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionDeploymentProfile>>([new("prod", "Production", "production", true)]);

        public Task<IReadOnlyList<CompanionDeploymentRun>> GetDeploymentHistoryAsync(string repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionDeploymentRun>>([]);

        public Task<CompanionDeploymentPreview> DryRunAsync(CompanionDeploymentRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new CompanionDeploymentPreview(
                "preview-1",
                request.RepositoryId,
                request.ProfileId,
                request.Paths.Select(path => new CompanionDeploymentOperation(path, "/remote/" + path, "upload")).ToArray(),
                DateTimeOffset.UtcNow));

        public Task<CompanionDeploymentRun> StartDeploymentAsync(string previewId, bool confirmed, CancellationToken cancellationToken)
        {
            StartCount++;
            return Task.FromResult(new CompanionDeploymentRun(
                "deploy-1", "repo-1", "prod", "running", DateTimeOffset.UtcNow, null, null));
        }

        public Task<CompanionDeploymentRun> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken)
        {
            RefreshCount++;
            return Task.FromResult(new CompanionDeploymentRun(
                deploymentId, "repo-1", "prod", "succeeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null));
        }
    }
}
