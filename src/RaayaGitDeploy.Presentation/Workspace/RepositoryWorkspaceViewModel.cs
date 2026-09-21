using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Core.Review;

namespace RaayaGitDeploy.Presentation.Workspace;

public partial class RepositoryWorkspaceViewModel : ObservableObject
{
    public const long MaxTextPreviewBytes = 256 * 1024;
    private const int RecentCommitLimit = 100;

    private readonly IGitRepositoryService _repositoryService;
    private ReviewSession? _reviewSession;

    [ObservableProperty] private WorkspaceSection selectedSection = WorkspaceSection.Changes;
    [ObservableProperty] private string? repositoryPath;
    [ObservableProperty] private string? branchName;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(AbbreviatedHeadSha))] private string? headSha;
    [ObservableProperty] private string? baseRef;
    [ObservableProperty] private string? selectedDiffText;
    [ObservableProperty] private GitCommitInfo? selectedCommit;
    [ObservableProperty] private GitChange? selectedCommitFile;
    [ObservableProperty] private string? selectedCommitDiffText;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public RepositoryWorkspaceViewModel(IGitRepositoryService repositoryService) =>
        _repositoryService = repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));

    public ObservableCollection<ChangeItemViewModel> Changes { get; } = new();
    public ObservableCollection<GitCommitInfo> Commits { get; } = new();
    public ObservableCollection<GitChange> CommitChanges { get; } = new();

    public string? AbbreviatedHeadSha => string.IsNullOrEmpty(HeadSha) ? null : HeadSha[..Math.Min(8, HeadSha.Length)];

    public void ClearError() => ErrorMessage = null;
    public void ReportError(Exception exception) { ArgumentNullException.ThrowIfNull(exception); ErrorMessage = exception.Message; }

    public Task LoadRepositoryAsync(string path, CancellationToken cancellationToken = default) => OpenRepositoryAsync(path, cancellationToken);

    public async Task OpenRepositoryAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await ExecuteAsync(async () =>
        {
            // Resolve the candidate repository completely before replacing the current workspace. A failed
            // FolderPicker selection (not a Git repository, inaccessible path, etc.) must not destroy the
            // repository/review state the user was already working with.
            var context = await _repositoryService.GetContextAsync(path, cancellationToken);
            var changes = await _repositoryService.GetWorkingTreeChangesAsync(context.RootPath, cancellationToken);
            var commits = await _repositoryService.GetRecentCommitsAsync(context.RootPath, RecentCommitLimit, cancellationToken);

            ClearRepositoryState();
            RepositoryPath = context.RootPath;
            BranchName = context.BranchName;
            HeadSha = context.HeadSha;
            SetWorkingTreeChanges(changes);
            foreach (var commit in commits) Commits.Add(commit);
        }, cancellationToken);
    }

    public async Task SelectCommitAsync(GitCommitInfo commit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commit);
        var path = GetRequiredRepositoryPath();
        ClearCommitSelection();
        SelectedCommit = commit;
        await ExecuteAsync(async () =>
        {
            var changes = await _repositoryService.GetCommitChangesAsync(path, commit.Sha, cancellationToken);
            foreach (var change in changes) CommitChanges.Add(change);
        }, cancellationToken);
    }

    public async Task SelectCommitFileAsync(GitChange file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var path = GetRequiredRepositoryPath();
        var commit = SelectedCommit ?? throw new InvalidOperationException("Select a commit before loading its diff.");
        SelectedCommitFile = null;
        SelectedCommitDiffText = null;
        await ExecuteAsync(async () =>
        {
            SelectedCommitDiffText = await _repositoryService.GetCommitFileDiffAsync(path, commit.Sha, file.Path, cancellationToken);
            SelectedCommitFile = file;
        }, cancellationToken);
    }

    public async Task CompareSinceAsync(string baseRef, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseRef);
        var path = GetRequiredRepositoryPath();
        var normalized = baseRef.Trim();
        await ExecuteAsync(async () =>
        {
            BaseRef = null; SelectedDiffText = null; ClearChanges();
            var changes = await _repositoryService.GetChangesSinceAsync(path, new GitComparisonRequest(normalized), cancellationToken);
            BaseRef = normalized; SetChanges(changes);
        }, cancellationToken);
    }

    public async Task LoadDiffAsync(ChangeItemViewModel item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var path = GetRequiredRepositoryPath();
        await ExecuteAsync(async () =>
        {
            SelectedDiffText = null;
            SelectedDiffText = await _repositoryService.GetDiffAsync(path, item.Path, BaseRef, MaxTextPreviewBytes, cancellationToken);
        }, cancellationToken);
    }

    private async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = null;
        try { await operation(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    private void ClearRepositoryState()
    {
        RepositoryPath = null; BranchName = null; HeadSha = null; BaseRef = null; SelectedDiffText = null;
        ClearChanges(); Commits.Clear(); ClearCommitSelection();
    }

    private void ClearCommitSelection()
    {
        SelectedCommit = null; CommitChanges.Clear(); SelectedCommitFile = null; SelectedCommitDiffText = null;
    }

    private void SetWorkingTreeChanges(IReadOnlyList<GitWorkingTreeChange> changes)
    {
        var domainChanges = changes.Select(static change => new GitChange(change.Path, change.Kind, change.OriginalPath)).ToArray();
        var flagsByPath = changes.ToDictionary(static change => change.Path, static change => (change.IsStaged, change.IsUnstaged), StringComparer.Ordinal);
        SetChanges(domainChanges, item => flagsByPath.TryGetValue(item.Path, out var flags) ? flags : default);
    }

    private void SetChanges(IReadOnlyList<GitChange> changes, Func<ReviewItem, (bool IsStaged, bool IsUnstaged)>? flagsProvider = null)
    {
        _reviewSession = ReviewSession.Create(changes); Changes.Clear();
        foreach (var item in _reviewSession.Items)
        {
            var flags = flagsProvider?.Invoke(item) ?? default;
            Changes.Add(new ChangeItemViewModel(_reviewSession, item, flags.IsStaged, flags.IsUnstaged));
        }
    }

    private void ClearChanges() { _reviewSession = null; Changes.Clear(); }
    private string GetRequiredRepositoryPath() => !string.IsNullOrWhiteSpace(RepositoryPath) ? RepositoryPath : throw new InvalidOperationException("Open a Git repository before running this operation.");
}
