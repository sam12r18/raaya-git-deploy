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

        Assert.Equal(CompanionShellScreen.Repositories, shell.Screen);
        Assert.False(shell.CanOpenDeployment);

        await workflow.LoadRepositoriesAsync(CancellationToken.None);
        await workflow.SelectRepositoryAsync("repo-1", CancellationToken.None);
        shell.OpenProfiles();
        Assert.Equal(CompanionShellScreen.Profiles, shell.Screen);
        Assert.False(shell.CanOpenDeployment);

        workflow.SelectProfile("prod");
        shell.OpenDeployment();
        Assert.Equal(CompanionShellScreen.Deployment, shell.Screen);
        Assert.True(shell.CanOpenDeployment);
    }

    [Fact]
    public async Task Shell_ExposesDeploymentWorkflowActivityForPresentation()
    {
        var workflow = new CompanionDeploymentWorkflow(new FakeApi());
        var shell = new CompanionShellState(workflow);
        await workflow.LoadRepositoriesAsync(CancellationToken.None);
        await workflow.SelectRepositoryAsync("repo-1", CancellationToken.None);
        workflow.SelectProfile("prod");

        await workflow.DryRunAsync(["src/app.cs"], CancellationToken.None);

        Assert.Equal(CompanionDeploymentActivityState.Ready, shell.DeploymentActivityState);
        Assert.Equal("Dry Run ready for review.", shell.DeploymentActivityMessage);
        Assert.True(shell.CanStartDeployment);
    }

    [Fact]
    public async Task Shell_LoadsAuthorizedHistoryDetailBeforeOpeningDetailScreen()
    {
        var api = new FakeApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        var shell = new CompanionShellState(workflow);

        await workflow.LoadRepositoriesAsync(CancellationToken.None);
        await workflow.SelectRepositoryAsync("repo-1", CancellationToken.None);
        await workflow.LoadHistoryAsync(CancellationToken.None);

        shell.OpenHistory();
        await shell.OpenHistoryDetailAsync("deploy-1", CancellationToken.None);

        Assert.Equal(CompanionShellScreen.HistoryDetail, shell.Screen);
        Assert.Equal("deploy-1", workflow.SelectedHistoryRun?.Id);
        Assert.Equal("deploy-1", api.LastDeploymentDetailId);
        Assert.False(shell.IsBusy);
        Assert.False(shell.CanRetry);
        Assert.Null(shell.LastError);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            shell.OpenHistoryDetailAsync("foreign-deploy", CancellationToken.None));
    }

    [Fact]
    public async Task Shell_RemainsOnHistoryAndExposesRetryableErrorWhenAuthorizedDetailLoadFails()
    {
        var api = new FakeApi { FailDeploymentDetail = true };
        var workflow = new CompanionDeploymentWorkflow(api);
        var shell = new CompanionShellState(workflow);

        await workflow.LoadRepositoriesAsync(CancellationToken.None);
        await workflow.SelectRepositoryAsync("repo-1", CancellationToken.None);
        await workflow.LoadHistoryAsync(CancellationToken.None);
        shell.OpenHistory();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            shell.OpenHistoryDetailAsync("deploy-1", CancellationToken.None));

        Assert.Equal(CompanionShellScreen.History, shell.Screen);
        Assert.Null(workflow.SelectedHistoryRun);
        Assert.Equal("deploy-1", api.LastDeploymentDetailId);
        Assert.False(shell.IsBusy);
        Assert.True(shell.CanRetry);
        Assert.Equal("Agent unavailable.", shell.LastError);

        shell.ClearError();
        Assert.False(shell.CanRetry);
        Assert.Null(shell.LastError);
    }

    [Fact]
    public async Task Shell_CancelledDetailLoadIsNotPresentedAsRetryableFailure()
    {
        var api = new FakeApi { CancelDeploymentDetail = true };
        var workflow = new CompanionDeploymentWorkflow(api);
        var shell = new CompanionShellState(workflow);

        await workflow.LoadRepositoriesAsync(CancellationToken.None);
        await workflow.SelectRepositoryAsync("repo-1", CancellationToken.None);
        await workflow.LoadHistoryAsync(CancellationToken.None);
        shell.OpenHistory();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            shell.OpenHistoryDetailAsync("deploy-1", CancellationToken.None));

        Assert.Equal(CompanionShellScreen.History, shell.Screen);
        Assert.False(shell.IsBusy);
        Assert.False(shell.CanRetry);
        Assert.Null(shell.LastError);
    }

    private sealed class FakeApi : ICompanionDeploymentApi
    {
        public string? LastDeploymentDetailId { get; private set; }
        public bool FailDeploymentDetail { get; init; }
        public bool CancelDeploymentDetail { get; init; }

        public Task<IReadOnlyList<CompanionRepository>> GetRepositoriesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionRepository>>([new("repo-1", "Repo", "main", "abc123", true)]);

        public Task<IReadOnlyList<CompanionDeploymentProfile>> GetProfilesAsync(string repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionDeploymentProfile>>([new("prod", "Production", "production", true)]);

        public Task<IReadOnlyList<CompanionDeploymentRun>> GetDeploymentHistoryAsync(string repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionDeploymentRun>>([
                new("deploy-1", repositoryId, "prod", "succeeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null)
            ]);

        public Task<CompanionDeploymentPreview> DryRunAsync(CompanionDeploymentRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new CompanionDeploymentPreview(
                "preview-1",
                request.RepositoryId,
                request.ProfileId,
                request.Paths.Select(path => new CompanionDeploymentOperation(path, "/remote/" + path, "upload")).ToArray(),
                DateTimeOffset.UtcNow));

        public Task<CompanionDeploymentRun> StartDeploymentAsync(string previewId, bool confirmed, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CompanionDeploymentRun> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken)
        {
            LastDeploymentDetailId = deploymentId;
            if (CancelDeploymentDetail)
                throw new OperationCanceledException(cancellationToken);
            if (FailDeploymentDetail)
                throw new HttpRequestException("Agent unavailable.");

            return Task.FromResult(new CompanionDeploymentRun(deploymentId, "repo-1", "prod", "succeeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null));
        }
    }
}
