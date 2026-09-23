using System.Net.Http;
using RaayaGitDeploy.Android.Core.Api;
using RaayaGitDeploy.Android.Core.Deployment;
using RaayaGitDeploy.Core.Companion;

namespace RaayaGitDeploy.Android.Core.Tests.Deployment;

public sealed class CompanionDeploymentFailureStateTests
{
    [Fact]
    public async Task DryRun_NetworkFailure_ExposesOfflineState()
    {
        var workflow = new CompanionDeploymentWorkflow(new FailureApi(new HttpRequestException("agent unavailable")));
        await PrepareAsync(workflow);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            workflow.DryRunAsync(["src/app.cs"], TestContext.Current.CancellationToken));

        Assert.Equal(CompanionDeploymentActivityState.Offline, workflow.ActivityState);
        Assert.Contains("unreachable", workflow.ActivityMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(workflow.Preview);
        Assert.Null(workflow.CurrentRun);
    }

    [Fact]
    public async Task DryRun_RateLimit_ExposesRetryDelayWithoutLosingSelection()
    {
        var workflow = new CompanionDeploymentWorkflow(new FailureApi(new CompanionRateLimitedException(TimeSpan.FromSeconds(27))));
        await PrepareAsync(workflow);

        await Assert.ThrowsAsync<CompanionRateLimitedException>(() =>
            workflow.DryRunAsync(["src/app.cs"], TestContext.Current.CancellationToken));

        Assert.Equal(CompanionDeploymentActivityState.RateLimited, workflow.ActivityState);
        Assert.Contains("27 seconds", workflow.ActivityMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("repo-1", workflow.SelectedRepository?.Id);
        Assert.Equal("prod", workflow.SelectedProfile?.Id);
        Assert.Null(workflow.Preview);
    }

    private static async Task PrepareAsync(CompanionDeploymentWorkflow workflow)
    {
        await workflow.LoadRepositoriesAsync(TestContext.Current.CancellationToken);
        await workflow.SelectRepositoryAsync("repo-1", TestContext.Current.CancellationToken);
        workflow.SelectProfile("prod");
    }

    private sealed class FailureApi(Exception failure) : ICompanionDeploymentApi
    {
        public Task<IReadOnlyList<CompanionRepository>> GetRepositoriesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionRepository>>([new("repo-1", "Repo", "main", "abc123", true)]);

        public Task<IReadOnlyList<CompanionDeploymentProfile>> GetProfilesAsync(string repositoryId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanionDeploymentProfile>>([new("prod", "Production", "production", true)]);

        public Task<CompanionDeploymentPreview> DryRunAsync(CompanionDeploymentRequest request, CancellationToken cancellationToken) =>
            Task.FromException<CompanionDeploymentPreview>(failure);

        public Task<CompanionDeploymentRun> StartDeploymentAsync(string previewId, bool confirmed, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompanionDeploymentRun> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CompanionDeploymentRun>> GetDeploymentHistoryAsync(string repositoryId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
