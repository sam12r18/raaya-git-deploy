using RaayaGitDeploy.Android.Core.Api;
using RaayaGitDeploy.Android.Core.Deployment;
using RaayaGitDeploy.Android.Core.Shell;
using RaayaGitDeploy.Core.Companion;

namespace RaayaGitDeploy.Android.Core.Tests.Shell;

public sealed class CompanionShellPollingRateLimitTests
{
    [Fact]
    public async Task Polling_RateLimited_PreservesRetryAfterAndRunningDeployment()
    {
        var api = new RateLimitedPollingApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        var shell = new CompanionShellState(workflow);
        var cancellationToken = TestContext.Current.CancellationToken;

        await workflow.LoadRepositoriesAsync(cancellationToken);
        await workflow.SelectRepositoryAsync("repo-1", cancellationToken);
        workflow.SelectProfile("prod");
        shell.OpenDeployment();
        await shell.DryRunAsync(["src/app.cs"], cancellationToken);
        await shell.StartDeploymentAsync(confirmed: true, cancellationToken);

        var exception = await Assert.ThrowsAsync<CompanionRateLimitedException>(() =>
            shell.PollDeploymentUntilTerminalAsync(5, TimeSpan.Zero, cancellationToken));

        Assert.Equal(TimeSpan.FromSeconds(12), exception.RetryAfter);
        Assert.False(shell.IsBusy);
        Assert.Equal(TimeSpan.FromSeconds(12), shell.RetryAfter);
        Assert.NotNull(shell.LastError);
        Assert.True(shell.CanRetry);
        Assert.True(shell.CanRefreshDeployment);
        Assert.Equal("running", shell.CurrentDeploymentState);
    }

    private sealed class RateLimitedPollingApi : ICompanionDeploymentApi
    {
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

        public Task<CompanionDeploymentRun> StartDeploymentAsync(string previewId, bool confirmed, CancellationToken cancellationToken) =>
            Task.FromResult(new CompanionDeploymentRun(
                "deploy-1", "repo-1", "prod", "running", DateTimeOffset.UtcNow, null, null));

        public Task<CompanionDeploymentRun> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken) =>
            Task.FromException<CompanionDeploymentRun>(new CompanionRateLimitedException(TimeSpan.FromSeconds(12)));
    }
}
