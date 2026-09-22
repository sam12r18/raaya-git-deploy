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
        await PrepareAsync(workflow);
        var preview = await workflow.DryRunAsync(["src/app.cs"], TestContext.Current.CancellationToken);
        Assert.Equal("preview-1", preview.Id);
        Assert.NotNull(api.LastRequest);
        Assert.Equal("repo-1", api.LastRequest!.RepositoryId);
        Assert.Equal("prod", api.LastRequest.ProfileId);
        Assert.True(api.LastRequest.DryRun);
    }

    [Fact]
    public async Task DryRun_RejectsPreviewFromAnotherRepository_AndClearsPreviewState()
    {
        var api = new FakeApi { ReturnForeignPreviewRepository = true };
        var workflow = new CompanionDeploymentWorkflow(api);
        await PrepareAsync(workflow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.DryRunAsync(["src/app.cs"], TestContext.Current.CancellationToken));
        Assert.Null(workflow.Preview);
        Assert.Null(workflow.CurrentRun);
    }

    [Fact]
    public async Task DryRun_RejectsPreviewFromAnotherProfile_AndClearsPreviewState()
    {
        var api = new FakeApi { ReturnForeignPreviewProfile = true };
        var workflow = new CompanionDeploymentWorkflow(api);
        await PrepareAsync(workflow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.DryRunAsync(["src/app.cs"], TestContext.Current.CancellationToken));
        Assert.Null(workflow.Preview);
        Assert.Null(workflow.CurrentRun);
    }

    [Fact]
    public async Task LoadHistory_UsesSelectedRepositoryAndKeepsOnlyScopedRuns()
    {
        var api = new FakeApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        await PrepareAsync(workflow);
        var history = await workflow.LoadHistoryAsync(TestContext.Current.CancellationToken);
        Assert.Equal("repo-1", api.LastHistoryRepositoryId);
        Assert.Single(history);
        Assert.Equal("history-1", history[0].Id);
        Assert.Same(history, workflow.History);
    }

    [Fact]
    public async Task LoadHistory_RejectsRunFromAnotherRepository()
    {
        var api = new FakeApi { ReturnForeignHistory = true };
        var workflow = new CompanionDeploymentWorkflow(api);
        await PrepareAsync(workflow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.LoadHistoryAsync(TestContext.Current.CancellationToken));
        Assert.Empty(workflow.History);
    }

    [Fact]
    public async Task StartDeployment_RequiresConfirmationWhenProfileRequiresIt()
    {
        var api = new FakeApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        await PrepareAsync(workflow);
        await workflow.DryRunAsync(["src/app.cs"], TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.StartDeploymentAsync(false, TestContext.Current.CancellationToken));
        Assert.Equal(0, api.StartCalls);
    }

    [Fact]
    public async Task StartDeployment_RejectsRunFromAnotherRepository()
    {
        var api = new FakeApi { ReturnForeignStartRun = true };
        var workflow = new CompanionDeploymentWorkflow(api);
        await PrepareAsync(workflow);
        await workflow.DryRunAsync(["src/app.cs"], TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.StartDeploymentAsync(true, TestContext.Current.CancellationToken));
        Assert.Null(workflow.CurrentRun);
    }

    [Fact]
    public async Task RefreshDeployment_UsesAgentRunIdAndStopsPollingAfterTerminalResult()
    {
        var api = new FakeApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        await PrepareAsync(workflow);
        await workflow.DryRunAsync(["src/app.cs"], TestContext.Current.CancellationToken);
        await workflow.StartDeploymentAsync(true, TestContext.Current.CancellationToken);
        var refreshed = await workflow.RefreshDeploymentAsync(TestContext.Current.CancellationToken);
        var terminalRefresh = await workflow.RefreshDeploymentAsync(TestContext.Current.CancellationToken);
        Assert.Equal("run-1", api.LastDeploymentId);
        Assert.Equal("succeeded", refreshed.State);
        Assert.True(workflow.IsCurrentRunTerminal);
        Assert.Same(refreshed, workflow.CurrentRun);
        Assert.Same(refreshed, terminalRefresh);
        Assert.Equal(1, api.GetDeploymentCalls);
    }

    [Fact]
    public async Task RefreshDeployment_RejectsRunFromAnotherRepository_AndPreservesCurrentRun()
    {
        var api = new FakeApi { ReturnForeignRefreshRun = true };
        var workflow = new CompanionDeploymentWorkflow(api);
        await PrepareAsync(workflow);
        await workflow.DryRunAsync(["src/app.cs"], TestContext.Current.CancellationToken);
        var started = await workflow.StartDeploymentAsync(true, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.RefreshDeploymentAsync(TestContext.Current.CancellationToken));
        Assert.Same(started, workflow.CurrentRun);
    }

    [Fact]
    public async Task PollUntilTerminal_StopsAtConfiguredAttemptLimit()
    {
        var api = new FakeApi { KeepRunning = true };
        var workflow = new CompanionDeploymentWorkflow(api);
        await PrepareAsync(workflow);
        await workflow.DryRunAsync(["src/app.cs"], TestContext.Current.CancellationToken);
        await workflow.StartDeploymentAsync(true, TestContext.Current.CancellationToken);
        var run = await workflow.PollUntilTerminalAsync(3, TimeSpan.Zero, TestContext.Current.CancellationToken);
        Assert.Equal("running", run.State);
        Assert.False(workflow.IsCurrentRunTerminal);
        Assert.Equal(3, api.GetDeploymentCalls);
    }

    private static async Task PrepareAsync(CompanionDeploymentWorkflow workflow)
    {
        await workflow.LoadRepositoriesAsync(TestContext.Current.CancellationToken);
        await workflow.SelectRepositoryAsync("repo-1", TestContext.Current.CancellationToken);
        workflow.SelectProfile("prod");
    }

    private sealed class FakeApi : ICompanionDeploymentApi
    {
        public CompanionDeploymentRequest? LastRequest { get; private set; }
        public string? LastDeploymentId { get; private set; }
        public string? LastHistoryRepositoryId { get; private set; }
        public int StartCalls { get; private set; }
        public int GetDeploymentCalls { get; private set; }
        public bool KeepRunning { get; init; }
        public bool ReturnForeignHistory { get; init; }
        public bool ReturnForeignStartRun { get; init; }
        public bool ReturnForeignRefreshRun { get; init; }
        public bool ReturnForeignPreviewRepository { get; init; }
        public bool ReturnForeignPreviewProfile { get; init; }

        public Task<IReadOnlyList<CompanionRepository>> GetRepositoriesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionRepository>>([new("repo-1", "Repo", "main", "abc123", true)]);

        public Task<IReadOnlyList<CompanionDeploymentProfile>> GetProfilesAsync(string repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionDeploymentProfile>>([new("prod", "Production", "production", true)]);

        public Task<IReadOnlyList<CompanionDeploymentRun>> GetDeploymentHistoryAsync(string repositoryId, CancellationToken cancellationToken)
        {
            LastHistoryRepositoryId = repositoryId;
            var runRepositoryId = ReturnForeignHistory ? "repo-2" : repositoryId;
            return Task.FromResult<IReadOnlyList<CompanionDeploymentRun>>([
                new("history-1", runRepositoryId, "prod", "succeeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null)
            ]);
        }

        public Task<CompanionDeploymentPreview> DryRunAsync(CompanionDeploymentRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var repositoryId = ReturnForeignPreviewRepository ? "repo-2" : request.RepositoryId;
            var profileId = ReturnForeignPreviewProfile ? "staging" : request.ProfileId;
            return Task.FromResult(new CompanionDeploymentPreview("preview-1", repositoryId, profileId, [new("src/app.cs", "/app/src/app.cs", "upload")], DateTimeOffset.UtcNow));
        }

        public Task<CompanionDeploymentRun> StartDeploymentAsync(string previewId, bool confirmed, CancellationToken cancellationToken)
        {
            StartCalls++;
            var repositoryId = ReturnForeignStartRun ? "repo-2" : "repo-1";
            return Task.FromResult(new CompanionDeploymentRun("run-1", repositoryId, "prod", "queued", DateTimeOffset.UtcNow, null, null));
        }

        public Task<CompanionDeploymentRun> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken)
        {
            GetDeploymentCalls++;
            LastDeploymentId = deploymentId;
            var state = KeepRunning ? "running" : "succeeded";
            var repositoryId = ReturnForeignRefreshRun ? "repo-2" : "repo-1";
            return Task.FromResult(new CompanionDeploymentRun(deploymentId, repositoryId, "prod", state, DateTimeOffset.UtcNow, KeepRunning ? null : DateTimeOffset.UtcNow, null));
        }
    }
}
