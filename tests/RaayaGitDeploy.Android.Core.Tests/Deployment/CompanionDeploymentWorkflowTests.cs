using RaayaGitDeploy.Android.Core.Deployment;
using RaayaGitDeploy.Core.Companion;

namespace RaayaGitDeploy.Android.Core.Tests.Deployment;

public sealed class CompanionDeploymentWorkflowTests
{
    [Fact]
    public async Task DryRun_UsesOnlyAuthorizedRepositoryAndProfile()
    {
        var api = new FakeApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        await workflow.LoadRepositoriesAsync(TestContext.Current.CancellationToken);
        await workflow.SelectRepositoryAsync("repo-1", TestContext.Current.CancellationToken);
        workflow.SelectProfile("prod");

        var preview = await workflow.DryRunAsync(["src/app.cs"], TestContext.Current.CancellationToken);

        Assert.Equal("preview-1", preview.Id);
        Assert.NotNull(api.LastRequest);
        Assert.Equal("repo-1", api.LastRequest!.RepositoryId);
        Assert.Equal("prod", api.LastRequest.ProfileId);
        Assert.True(api.LastRequest.DryRun);
    }

    [Fact]
    public async Task StartDeployment_RequiresConfirmationWhenProfileRequiresIt()
    {
        var api = new FakeApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        await workflow.LoadRepositoriesAsync(TestContext.Current.CancellationToken);
        await workflow.SelectRepositoryAsync("repo-1", TestContext.Current.CancellationToken);
        workflow.SelectProfile("prod");
        await workflow.DryRunAsync(["src/app.cs"], TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.StartDeploymentAsync(false, TestContext.Current.CancellationToken));
        Assert.Equal(0, api.StartCalls);
    }

    [Fact]
    public async Task RefreshDeployment_UsesAgentRunIdAndStopsPollingAfterTerminalResult()
    {
        var api = new FakeApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        await workflow.LoadRepositoriesAsync(TestContext.Current.CancellationToken);
        await workflow.SelectRepositoryAsync("repo-1", TestContext.Current.CancellationToken);
        workflow.SelectProfile("prod");
        await workflow.DryRunAsync(["src/app.cs"], TestContext.Current.CancellationToken);
        await workflow.StartDeploymentAsync(true, TestContext.Current.CancellationToken);

        var refreshed = await workflow.RefreshDeploymentAsync(TestContext.Current.CancellationToken);
        var terminalRefresh = await workflow.RefreshDeploymentAsync(TestContext.Current.CancellationToken);

        Assert.Equal("run-1", api.LastDeploymentId);
        Assert.Equal("succeeded", refreshed.Status);
        Assert.True(workflow.IsCurrentRunTerminal);
        Assert.Same(refreshed, workflow.CurrentRun);
        Assert.Same(refreshed, terminalRefresh);
        Assert.Equal(1, api.GetDeploymentCalls);
    }

    private sealed class FakeApi : ICompanionDeploymentApi
    {
        public CompanionDeploymentRequest? LastRequest { get; private set; }
        public string? LastDeploymentId { get; private set; }
        public int StartCalls { get; private set; }
        public int GetDeploymentCalls { get; private set; }

        public Task<IReadOnlyList<CompanionRepository>> GetRepositoriesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionRepository>>([new("repo-1", "Repo", "main", "abc123", true)]);

        public Task<IReadOnlyList<CompanionDeploymentProfile>> GetProfilesAsync(string repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionDeploymentProfile>>([new("prod", "Production", "production", true)]);

        public Task<CompanionDeploymentPreview> DryRunAsync(CompanionDeploymentRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new CompanionDeploymentPreview("preview-1", request.RepositoryId, request.ProfileId, [new("src/app.cs", "/app/src/app.cs", "upload")], DateTimeOffset.UtcNow));
        }

        public Task<CompanionDeploymentRun> StartDeploymentAsync(string previewId, bool confirmed, CancellationToken cancellationToken)
        {
            StartCalls++;
            return Task.FromResult(new CompanionDeploymentRun("run-1", "repo-1", "prod", "queued", DateTimeOffset.UtcNow, null, null));
        }

        public Task<CompanionDeploymentRun> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken)
        {
            GetDeploymentCalls++;
            LastDeploymentId = deploymentId;
            return Task.FromResult(new CompanionDeploymentRun(deploymentId, "repo-1", "prod", "succeeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null));
        }
    }
}
