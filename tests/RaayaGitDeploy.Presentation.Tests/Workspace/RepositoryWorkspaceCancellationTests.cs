using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.Presentation.Tests.Workspace;

public sealed class RepositoryWorkspaceCancellationTests
{
    [Fact]
    public async Task OpenRepositoryAsync_WhenCancelled_ResetsBusyStateWithoutShowingAnError()
    {
        var testCancellation = TestContext.Current.CancellationToken;
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(testCancellation);
        var service = new CancellingGitRepositoryService(operationCancellation.Token);
        var viewModel = new RepositoryWorkspaceViewModel(service);

        var operation = viewModel.OpenRepositoryAsync(@"I:\Projects\sample", operationCancellation.Token);
        await service.ContextRequested.Task.WaitAsync(testCancellation);
        operationCancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);

        Assert.False(viewModel.IsBusy);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Empty(viewModel.Changes);
    }

    private sealed class CancellingGitRepositoryService(CancellationToken operationCancellation) : IGitRepositoryService
    {
        public TaskCompletionSource ContextRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<GitRepositoryContext> GetContextAsync(string path, CancellationToken cancellationToken)
        {
            ContextRequested.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, operationCancellation);
            throw new InvalidOperationException("unreachable");
        }

        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(string path, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(string repositoryPath, GitComparisonRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, long maxUntrackedPreviewBytes, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
