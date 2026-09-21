using RaayaGitDeploy.Android.Core.Deployment;
using RaayaGitDeploy.Core.Companion;

namespace RaayaGitDeploy.Android.Core.Tests.Deployment;

public sealed class CompanionDeploymentHistoryReloadTests
{
    [Fact]
    public async Task LoadHistory_FailedReload_DoesNotLeaveStaleTimelineVisible()
    {
        var api = new FailingReloadApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        var cancellationToken = TestContext.Current.CancellationToken;
        await workflow.LoadRepositoriesAsync(cancellationToken);
        await workflow.SelectRepositoryAsync("repo-1", cancellationToken);
        await workflow.LoadHistoryAsync(cancellationToken);
        Assert.Single(workflow.History);

        api.FailHistory = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.LoadHistoryAsync(cancellationToken));

        Assert.Empty(workflow.History);
        Assert.Null(workflow.SelectedHistoryRun);
    }

    private sealed class FailingReloadApi : ICompanionDeploymentApi
    {
        public bool FailHistory { get; set; }

        public Task<IReadOnlyList<CompanionRepository>> GetRepositoriesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionRepository>>([new("repo-1", "Repo", "main", "abc123", true)]);

        public Task<IReadOnlyList<CompanionDeploymentProfile>> GetProfilesAsync(string repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionDeploymentProfile>>([new("prod", "Production", "production", true)]);

        public Task<IReadOnlyList<CompanionDeploymentRun>> GetDeploymentHistoryAsync(string repositoryId, CancellationToken cancellationToken)
        {
            if (FailHistory)
                throw new InvalidOperationException("Agent is offline.");

            return Task.FromResult<IReadOnlyList<CompanionDeploymentRun>>([
                new("history-1", repositoryId, "prod", "succeeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null)
            ]);
        }

        public Task<CompanionDeploymentRun> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompanionDeploymentPreview> DryRunAsync(CompanionDeploymentRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompanionDeploymentRun> StartDeploymentAsync(string previewId, bool confirmed, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
