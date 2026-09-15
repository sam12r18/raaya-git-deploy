using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.Presentation.Tests.Workspace;

public sealed class RepositoryWorkspaceConcurrencyTests
{
    [Fact]
    public async Task OpenRepositoryAsync_WhileBusy_DoesNotStartDuplicateGitOperation()
    {
        var service = new BlockingRepositoryService();
        var viewModel = new RepositoryWorkspaceViewModel(service);

        var firstOpen = viewModel.OpenRepositoryAsync(@"I:\Projects\first", CancellationToken.None);
        await service.FirstContextRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await viewModel.OpenRepositoryAsync(@"I:\Projects\second", CancellationToken.None);

        Assert.Equal(1, service.ContextRequestCount);

        service.ReleaseFirstContextRequest.TrySetResult();
        await firstOpen;
    }

    private sealed class BlockingRepositoryService : IGitRepositoryService
    {
        public TaskCompletionSource FirstContextRequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstContextRequest { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ContextRequestCount { get; private set; }

        public async Task<GitRepositoryContext> GetContextAsync(string path, CancellationToken cancellationToken)
        {
            ContextRequestCount++;
            FirstContextRequestStarted.TrySetResult();
            await ReleaseFirstContextRequest.Task.WaitAsync(cancellationToken);
            return new GitRepositoryContext(@"I:\Projects\first", "main", new string('a', 40));
        }

        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(string path, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitWorkingTreeChange>>([]);

        public Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(string repositoryPath, GitComparisonRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GitChange>>([]);

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, long maxUntrackedPreviewBytes, CancellationToken cancellationToken) =>
            Task.FromResult(string.Empty);
    }
}
