using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Presentation.Deployment;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.Presentation.Tests.Workspace;

public sealed class RepositoryOpenDeploymentStateTests
{
    [Fact]
    public async Task ReopeningSameRepository_PreservesDeploymentQueue()
    {
        var root = Path.Combine(Path.GetTempPath(), "raaya-open-same-repository");
        var git = new FakeGitRepositoryService();
        var workspace = new RepositoryWorkspaceViewModel(git);
        await workspace.OpenRepositoryAsync(root, CancellationToken.None);
        var deployment = await CreateDeploymentAsync();
        deployment.Queue.AddFile(Path.Combine(root, "app.js"));
        var coordinator = new RepositoryOpenCoordinator(new FakePicker(root + Path.DirectorySeparatorChar), workspace, deployment);

        await coordinator.OpenRepositoryAsync(CancellationToken.None);

        Assert.Single(deployment.Queue.Items);
        Assert.Equal(root + Path.DirectorySeparatorChar, git.ContextRequestedPath);
    }

    [Fact]
    public async Task OpeningDifferentRepository_ClearsDeploymentQueue()
    {
        var first = Path.Combine(Path.GetTempPath(), "raaya-open-repository-a");
        var second = Path.Combine(Path.GetTempPath(), "raaya-open-repository-b");
        var git = new FakeGitRepositoryService();
        var workspace = new RepositoryWorkspaceViewModel(git);
        await workspace.OpenRepositoryAsync(first, CancellationToken.None);
        var deployment = await CreateDeploymentAsync();
        deployment.Queue.AddFile(Path.Combine(first, "app.js"));
        var coordinator = new RepositoryOpenCoordinator(new FakePicker(second), workspace, deployment);

        await coordinator.OpenRepositoryAsync(CancellationToken.None);

        Assert.Empty(deployment.Queue.Items);
        Assert.Equal(second, workspace.RepositoryPath);
    }

    [Fact]
    public async Task OpeningInvalidRepository_PreservesWorkspaceAndDeploymentQueue()
    {
        var first = Path.Combine(Path.GetTempPath(), "raaya-open-valid-repository");
        var invalid = Path.Combine(Path.GetTempPath(), "raaya-open-invalid-repository");
        var git = new FakeGitRepositoryService();
        var workspace = new RepositoryWorkspaceViewModel(git);
        await workspace.OpenRepositoryAsync(first, CancellationToken.None);
        var deployment = await CreateDeploymentAsync();
        deployment.Queue.AddFile(Path.Combine(first, "app.js"));
        git.FailPath = invalid;
        var coordinator = new RepositoryOpenCoordinator(new FakePicker(invalid), workspace, deployment);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await coordinator.OpenRepositoryAsync(CancellationToken.None);
        });

        Assert.Equal(first, workspace.RepositoryPath);
        Assert.Single(deployment.Queue.Items);
    }

    private static async Task<DeploymentWorkspaceViewModel> CreateDeploymentAsync()
    {
        var profile = new ServerProfile("prod", "Production", "example.test", 22, "deploy", "/srv/app", ServerAuthenticationMode.SshKey, "key:prod");
        var transport = new FakeTransport();
        var servers = new ServersViewModel(new FakeStore(profile), transport);
        var planner = new DeploymentPlanner();
        var deployment = new DeploymentWorkspaceViewModel(new DeploymentQueueViewModel(), servers, new DeploymentDryRunViewModel(planner), planner, new DeploymentExecutor(transport), new FakeHistoryStore());
        await deployment.LoadServersAsync(CancellationToken.None);
        return deployment;
    }

    private sealed class FakePicker(string path) : IRepositoryFolderPicker
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(path);
    }

    private sealed class FakeGitRepositoryService : IGitRepositoryService
    {
        public string? ContextRequestedPath { get; private set; }
        public string? FailPath { get; set; }

        public Task<GitRepositoryContext> GetContextAsync(string path, CancellationToken cancellationToken)
        {
            ContextRequestedPath = path;
            if (string.Equals(path, FailPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Not a Git repository.");

            return Task.FromResult(new GitRepositoryContext(path, "main", new string('a', 40)));
        }
        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(string repositoryPath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GitWorkingTreeChange>>([]);
        public Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(string repositoryPath, GitComparisonRequest request, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GitChange>>([]);
        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, CancellationToken cancellationToken) => Task.FromResult(string.Empty);
        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, long maxTextBytes, CancellationToken cancellationToken) => Task.FromResult(string.Empty);
    }

    private sealed class FakeStore(params ServerProfile[] profiles) : IServerProfileStore
    {
        public Task<IReadOnlyList<ServerProfile>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ServerProfile>>(profiles);
        public Task UpsertAsync(ServerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class FakeHistoryStore : IDeploymentHistoryStore
    {
        public Task<IReadOnlyList<DeploymentHistoryEntry>> LoadAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DeploymentHistoryEntry>>([]);
        public Task AppendAsync(DeploymentHistoryEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class FakeTransport : IRemoteTransport
    {
        public Task TestConnectionAsync(ServerProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task UploadAsync(ServerProfile profile, string localPath, string remotePath, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(ServerProfile profile, string remotePath, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
