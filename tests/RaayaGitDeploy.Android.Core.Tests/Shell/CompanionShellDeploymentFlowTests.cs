using RaayaGitDeploy.Android.Core.Deployment;
using RaayaGitDeploy.Android.Core.Shell;
using RaayaGitDeploy.Core.Companion;

namespace RaayaGitDeploy.Android.Core.Tests.Shell;

public sealed class CompanionShellDeploymentFlowTests
{
    [Fact]
    public async Task Shell_DryRunStartRefresh_ReachesTerminalResultWithoutCredentials()
    {
        var api = new FlowApi();
        var workflow = new CompanionDeploymentWorkflow(api);
        var shell = new CompanionShellState(workflow);
        var cancellationToken = TestContext.Current.CancellationToken;

        await workflow.LoadRepositoriesAsync(cancellationToken);
        await workflow.SelectRepositoryAsync("repo-1", cancellationToken);
        workflow.SelectProfile("prod");
        shell.OpenDeployment();

        var preview = await shell.DryRunAsync(["src/app.cs"], cancellationToken);

        Assert.Equal("preview-1", preview.Id);
        Assert.True(shell.CanStartDeployment);
        Assert.False(shell.CanRefreshDeployment);

        var started = await shell.StartDeploymentAsync(confirmed: true, cancellationToken);

        Assert.Equal("deploy-1", started.Id);
        Assert.False(shell.CanStartDeployment);
        Assert.True(shell.CanRefreshDeployment);
        Assert.False(shell.IsDeploymentTerminal);

        var completed = await shell.RefreshDeploymentAsync(cancellationToken);

        Assert.Equal("succeeded", completed.State);
        Assert.Equal("succeeded", shell.CurrentDeploymentState);
        Assert.True(shell.IsDeploymentTerminal);
        Assert.False(shell.CanRefreshDeployment);
        Assert.False(shell.IsBusy);
        Assert.Null(shell.LastError);
        Assert.Equal(1, api.StartCount);
        Assert.Equal(1, api.RefreshCount);
    }

    [Fact]
    public async Task Shell_PollDeploymentUntilTerminal_RefreshesUntilSucceeded()
    {
        var api = new FlowApi(["running", "running", "succeeded"]);
        var workflow = new CompanionDeploymentWorkflow(api);
        var shell = new CompanionShellState(workflow);
        var cancellationToken = TestContext.Current.CancellationToken;

        await workflow.LoadRepositoriesAsync(cancellationToken);
        await workflow.SelectRepositoryAsync("repo-1", cancellationToken);
        workflow.SelectProfile("prod");
        shell.OpenDeployment();
        await shell.DryRunAsync(["src/app.cs"], cancellationToken);
        await shell.StartDeploymentAsync(confirmed: true, cancellationToken);

        var completed = await shell.PollDeploymentUntilTerminalAsync(5, TimeSpan.Zero, cancellationToken);

        Assert.Equal("succeeded", completed.State);
        Assert.True(shell.IsDeploymentTerminal);
        Assert.False(shell.CanRefreshDeployment);
        Assert.False(shell.IsBusy);
        Assert.Null(shell.LastError);
        Assert.Equal(3, api.RefreshCount);
    }

    private sealed class FlowApi : ICompanionDeploymentApi
    {
        private readonly Queue<string> _refreshStates;

        public FlowApi(IEnumerable<string>? refreshStates = null) =>
            _refreshStates = new Queue<string>(refreshStates ?? ["succeeded"]);

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
            Assert.Equal("preview-1", previewId);
            Assert.True(confirmed);
            StartCount++;
            return Task.FromResult(new CompanionDeploymentRun(
                "deploy-1", "repo-1", "prod", "running", DateTimeOffset.UtcNow, null, null));
        }

        public Task<CompanionDeploymentRun> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken)
        {
            Assert.Equal("deploy-1", deploymentId);
            RefreshCount++;
            var state = _refreshStates.Count > 0 ? _refreshStates.Dequeue() : "succeeded";
            var completedAt = string.Equals(state, "succeeded", StringComparison.OrdinalIgnoreCase) ? DateTimeOffset.UtcNow : null;
            return Task.FromResult(new CompanionDeploymentRun(
                deploymentId, "repo-1", "prod", state, DateTimeOffset.UtcNow, completedAt, null));
        }
    }
}
