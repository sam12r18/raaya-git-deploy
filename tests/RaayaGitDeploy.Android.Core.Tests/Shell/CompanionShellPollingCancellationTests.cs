using RaayaGitDeploy.Android.Core.Deployment;
using RaayaGitDeploy.Android.Core.Shell;
using RaayaGitDeploy.Core.Companion;

namespace RaayaGitDeploy.Android.Core.Tests.Shell;

public sealed class CompanionShellPollingCancellationTests
{
    [Fact]
    public async Task Polling_AgentCancellation_DoesNotBecomeRetryableError()
    {
        var api = new CancellingPollingApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        var shell = new CompanionShellState(workflow);
        var cancellationToken = TestContext.Current.CancellationToken;

        await workflow.LoadRepositoriesAsync(cancellationToken);
        await workflow.SelectRepositoryAsync("repo-1", cancellationToken);
        workflow.SelectProfile("prod");
        shell.OpenDeployment();
        await shell.DryRunAsync(["src/app.cs"], cancellationToken);
        await shell.StartDeploymentAsync(confirmed: true, cancellationToken);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            shell.PollDeploymentUntilTerminalAsync(5, TimeSpan.Zero, cancellationToken));

        Assert.False(shell.IsBusy);
        Assert.Null(shell.LastError);
        Assert.Null(shell.RetryAfter);
        Assert.False(shell.CanRetry);
        Assert.True(shell.CanRefreshDeployment);
    }

    private sealed class CancellingPollingApi : ICompanionDeploymentApi
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
            Task.FromException<CompanionDeploymentRun>(new OperationCanceledException("Agent cancelled polling."));
    }
}
