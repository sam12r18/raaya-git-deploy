using RaayaGitDeploy.Android.Core.Deployment;
using RaayaGitDeploy.Core.Companion;

namespace RaayaGitDeploy.Android.Core.Tests.Deployment;

public sealed class CompanionDeploymentHistoryBoundaryTests
{
    [Fact]
    public async Task LoadHistoryDetail_RejectsForeignRepositoryResponse()
    {
        var api = new ForeignDetailApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        await workflow.LoadRepositoriesAsync(TestContext.Current.CancellationToken);
        await workflow.SelectRepositoryAsync("repo-1", TestContext.Current.CancellationToken);
        await workflow.LoadHistoryAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.LoadHistoryDetailAsync("history-1", TestContext.Current.CancellationToken));

        Assert.Null(workflow.SelectedHistoryRun);
        Assert.Equal(1, api.DetailCalls);
    }

    private sealed class ForeignDetailApi : ICompanionDeploymentApi
    {
        public int DetailCalls { get; private set; }

        public Task<IReadOnlyList<CompanionRepository>> GetRepositoriesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionRepository>>([new("repo-1", "Repo", "main", "abc123", true)]);

        public Task<IReadOnlyList<CompanionDeploymentProfile>> GetProfilesAsync(string repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionDeploymentProfile>>([new("prod", "Production", "production", true)]);

        public Task<IReadOnlyList<CompanionDeploymentRun>> GetDeploymentHistoryAsync(string repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionDeploymentRun>>([
                new("history-1", repositoryId, "prod", "succeeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null)
            ]);

        public Task<CompanionDeploymentRun> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken)
        {
            DetailCalls++;
            return Task.FromResult(new CompanionDeploymentRun(deploymentId, "repo-2", "prod", "succeeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null));
        }

        public Task<CompanionDeploymentPreview> DryRunAsync(CompanionDeploymentRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompanionDeploymentRun> StartDeploymentAsync(string previewId, bool confirmed, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
