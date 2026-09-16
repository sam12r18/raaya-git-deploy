using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Core.Review;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.Presentation.Tests.Workspace;

public sealed class RepositoryWorkspaceViewModelTests
{
    [Fact]
    public async Task OpenRepositoryAsync_LoadsContextWorkingTreeAndReviewState()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = CreateService();
        var viewModel = new RepositoryWorkspaceViewModel(service);

        await viewModel.OpenRepositoryAsync(@"I:\Projects\sample\src", cancellationToken);

        Assert.Equal(@"I:\Projects\sample", viewModel.RepositoryPath);
        Assert.Equal("feature/ai-change", viewModel.BranchName);
        Assert.Equal("0123456789abcdef0123456789abcdef01234567", viewModel.HeadSha);
        Assert.Equal("01234567", viewModel.AbbreviatedHeadSha);
        Assert.False(viewModel.IsBusy);
        Assert.Null(viewModel.ErrorMessage);

        var change = Assert.Single(viewModel.Changes);
        Assert.Equal("src/App.cs", change.Path);
        Assert.Equal(GitChangeKind.Modified, change.Kind);
        Assert.Equal(ReviewState.Unreviewed, change.ReviewState);
        Assert.False(change.IsSelectedForDeployment);

        change.ReviewState = ReviewState.Approved;
        Assert.False(change.IsSelectedForDeployment);

        change.IsSelectedForDeployment = true;
        Assert.Equal(ReviewState.Approved, change.ReviewState);

        change.ReviewState = ReviewState.Excluded;
        Assert.False(change.IsSelectedForDeployment);

        Assert.Equal(@"I:\Projects\sample\src", service.ContextRequestedPath);
        Assert.Equal(@"I:\Projects\sample", service.WorkingTreeRequestedPath);
    }

    [Fact]
    public async Task CompareSinceAsync_ReplacesDisplayedChangesAndTracksBaseRef()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = CreateService();
        var viewModel = new RepositoryWorkspaceViewModel(service);
        await viewModel.OpenRepositoryAsync(@"I:\Projects\sample", cancellationToken);

        service.ComparedChanges =
        [
            new GitChange("src/Renamed.cs", GitChangeKind.Renamed, "src/Old.cs")
        ];

        await viewModel.CompareSinceAsync("HEAD~1", cancellationToken);

        var change = Assert.Single(viewModel.Changes);
        Assert.Equal("src/Renamed.cs", change.Path);
        Assert.Equal("src/Old.cs", change.OriginalPath);
        Assert.Equal("HEAD~1", viewModel.BaseRef);
        Assert.Equal("HEAD~1", service.ComparisonRequest?.BaseRef);
        Assert.Equal(@"I:\Projects\sample", service.CompareRequestedPath);
    }

    [Fact]
    public async Task CompareSinceAsync_FailureClearsStaleComparisonResults()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = CreateService();
        var viewModel = new RepositoryWorkspaceViewModel(service);
        await viewModel.OpenRepositoryAsync(@"I:\Projects\sample", cancellationToken);

        service.ComparedChanges = [new GitChange("src/Compared.cs", GitChangeKind.Modified)];
        await viewModel.CompareSinceAsync("HEAD~1", cancellationToken);
        Assert.Equal("HEAD~1", viewModel.BaseRef);
        Assert.Equal("src/Compared.cs", Assert.Single(viewModel.Changes).Path);

        service.CompareException = new InvalidOperationException("comparison failed");
        await viewModel.CompareSinceAsync("missing-ref", cancellationToken);

        Assert.Null(viewModel.BaseRef);
        Assert.Empty(viewModel.Changes);
        Assert.Null(viewModel.SelectedDiffText);
        Assert.Equal("comparison failed", viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task LoadDiffAsync_StoresDiffTextUsingCurrentComparisonMode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = CreateService();
        var viewModel = new RepositoryWorkspaceViewModel(service);
        await viewModel.OpenRepositoryAsync(@"I:\Projects\sample", cancellationToken);

        service.ComparedChanges = [new GitChange("src/App.cs", GitChangeKind.Modified)];
        service.DiffText = "@@ -1 +1 @@\n-old\n+new";
        await viewModel.CompareSinceAsync("HEAD~1", cancellationToken);

        await viewModel.LoadDiffAsync(Assert.Single(viewModel.Changes), cancellationToken);

        Assert.Equal(service.DiffText, viewModel.SelectedDiffText);
        Assert.Equal("src/App.cs", service.DiffRequestedPath);
        Assert.Equal("HEAD~1", service.DiffRequestedBaseRef);
    }

    [Fact]
    public async Task LoadDiffAsync_UsesPresentationPreviewSizeLimit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = CreateService();
        var viewModel = new RepositoryWorkspaceViewModel(service);
        await viewModel.OpenRepositoryAsync(@"I:\Projects\sample", cancellationToken);

        await viewModel.LoadDiffAsync(Assert.Single(viewModel.Changes), cancellationToken);

        Assert.Equal(
            RepositoryWorkspaceViewModel.MaxTextPreviewBytes,
            service.DiffRequestedMaxPreviewBytes);
    }

    [Fact]
    public async Task GitFailure_SetsErrorMessageAndAlwaysResetsBusyState()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var service = CreateService();
        service.ContextException = new InvalidOperationException("not a git repository");
        var viewModel = new RepositoryWorkspaceViewModel(service);

        await viewModel.OpenRepositoryAsync(@"I:\Broken", cancellationToken);

        Assert.Equal("not a git repository", viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
        Assert.Empty(viewModel.Changes);
    }

    private static StubGitRepositoryService CreateService() => new()
    {
        Context = new GitRepositoryContext(
            @"I:\Projects\sample",
            "feature/ai-change",
            "0123456789abcdef0123456789abcdef01234567"),
        WorkingTreeChanges =
        [
            new GitWorkingTreeChange(
                "src/App.cs",
                GitChangeKind.Modified,
                null,
                IsStaged: false,
                IsUnstaged: true)
        ]
    };

    private sealed class StubGitRepositoryService : IGitRepositoryService
    {
        public required GitRepositoryContext Context { get; init; }
        public IReadOnlyList<GitWorkingTreeChange> WorkingTreeChanges { get; init; } = [];
        public IReadOnlyList<GitChange> ComparedChanges { get; set; } = [];
        public string DiffText { get; set; } = string.Empty;
        public Exception? ContextException { get; set; }
        public Exception? CompareException { get; set; }
        public string? ContextRequestedPath { get; private set; }
        public string? WorkingTreeRequestedPath { get; private set; }
        public string? CompareRequestedPath { get; private set; }
        public GitComparisonRequest? ComparisonRequest { get; private set; }
        public string? DiffRequestedPath { get; private set; }
        public string? DiffRequestedBaseRef { get; private set; }
        public long? DiffRequestedMaxPreviewBytes { get; private set; }

        public Task<GitRepositoryContext> GetContextAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ContextRequestedPath = path;
            if (ContextException is not null) throw ContextException;
            return Task.FromResult(Context);
        }

        public Task<IReadOnlyList<GitWorkingTreeChange>> GetWorkingTreeChangesAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WorkingTreeRequestedPath = path;
            return Task.FromResult(WorkingTreeChanges);
        }

        public Task<IReadOnlyList<GitChange>> GetChangesSinceAsync(string repositoryPath, GitComparisonRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CompareRequestedPath = repositoryPath;
            ComparisonRequest = request;
            if (CompareException is not null) throw CompareException;
            return Task.FromResult(ComparedChanges);
        }

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiffRequestedPath = path;
            DiffRequestedBaseRef = baseRef;
            return Task.FromResult(DiffText);
        }

        public Task<string> GetDiffAsync(string repositoryPath, string path, string? baseRef, long maxUntrackedPreviewBytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiffRequestedPath = path;
            DiffRequestedBaseRef = baseRef;
            DiffRequestedMaxPreviewBytes = maxUntrackedPreviewBytes;
            return Task.FromResult(DiffText);
        }
    }
}
