using RaayaGitDeploy.Android.Core.Deployment;
using RaayaGitDeploy.Core.Companion;

namespace RaayaGitDeploy.Android.Core.Tests.Deployment;

public sealed class CompanionDeploymentHistoryDetailTests
{
    [Fact]
    public async Task LoadHistoryDetail_UsesOnlyRunFromSelectedRepositoryHistory()
    {
        var api = new HistoryApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        await PrepareAsync(workflow);

        var detail = await workflow.LoadHistoryDetailAsync("history-1", TestContext.Current.CancellationToken);

        Assert.Equal("history-1", api.LastDeploymentId);
        Assert.Equal("repo-1", detail.RepositoryId);
        Assert.Same(detail, workflow.SelectedHistoryRun);
    }

    [Fact]
    public async Task LoadHistoryDetail_RejectsUnknownRunBeforeCallingAgent()
    {
        var api = new HistoryApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        await PrepareAsync(workflow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.LoadHistoryDetailAsync("not-in-history", TestContext.Current.CancellationToken));

        Assert.Null(api.LastDeploymentId);
        Assert.Null(workflow.SelectedHistoryRun);
    }

    private static async Task PrepareAsync(CompanionDeploymentWorkflow workflow)
    {
        await workflow.LoadRepositoriesAsync(TestContext.Current.CancellationToken);
        await workflow.SelectRepositoryAsync("repo-1", TestContext.Current.CancellationToken);
        await workflow.LoadHistoryAsync(TestContext.Current.CancellationToken);
    }

    private sealed class HistoryApi : ICompanionDeploymentApi
    {
        public string? LastDeploymentId { get; private set; }

        public Task<IReadOnlyList<CompanionRepository>> GetRepositoriesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionRepository>>([new("repo-1", "Repo", "main", "abc123", true)]);

        public Task<IReadOnlyList<CompanionDeploymentProfile>> GetProfilesAsync(string repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionDeploymentProfile>>([]);

        public Task<IReadOnlyList<CompanionDeploymentRun>> GetDeploymentHistoryAsync(string repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionDeploymentRun>>([
                new("history-1", repositoryId, "prod", "succeeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null)
            ]);

        public Task<CompanionDeploymentRun> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken)
        {
            LastDeploymentId = deploymentId;
            return Task.FromResult(new CompanionDeploymentRun(deploymentId, "repo-1", "prod", "succeeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null));
        }

        public Task<CompanionDeploymentPreview> DryRunAsync(CompanionDeploymentRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompanionDeploymentRun> StartDeploymentAsync(string previewId, bool confirmed, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
