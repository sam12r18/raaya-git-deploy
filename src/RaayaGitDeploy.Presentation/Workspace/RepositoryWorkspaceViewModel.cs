using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Core.Review;

namespace RaayaGitDeploy.Presentation.Workspace;

public partial class RepositoryWorkspaceViewModel : ObservableObject
{
    public const long MaxTextPreviewBytes = 256 * 1024;

    private readonly IGitRepositoryService _repositoryService;
    private ReviewSession? _reviewSession;

    [ObservableProperty]
    private string? repositoryPath;

    [ObservableProperty]
    private string? branchName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AbbreviatedHeadSha))]
    private string? headSha;

    [ObservableProperty]
    private string? baseRef;

    [ObservableProperty]
    private string? selectedDiffText;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public RepositoryWorkspaceViewModel(IGitRepositoryService repositoryService)
    {
        _repositoryService = repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
    }

    public ObservableCollection<ChangeItemViewModel> Changes { get; } = new();

    public string? AbbreviatedHeadSha =>
        string.IsNullOrEmpty(HeadSha)
            ? null
            : HeadSha[..Math.Min(8, HeadSha.Length)];

    public void ClearError() => ErrorMessage = null;

    public void ReportError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ErrorMessage = exception.Message;
    }

    public Task LoadRepositoryAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        OpenRepositoryAsync(path, cancellationToken);

    public async Task OpenRepositoryAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await ExecuteAsync(
            async () =>
            {
                ClearRepositoryState();

                var context = await _repositoryService.GetContextAsync(path, cancellationToken);
                var changes = await _repositoryService.GetWorkingTreeChangesAsync(
                    context.RootPath,
                    cancellationToken);

                RepositoryPath = context.RootPath;
                BranchName = context.BranchName;
                HeadSha = context.HeadSha;

                SetWorkingTreeChanges(changes);
            },
            cancellationToken);
    }

    public async Task CompareSinceAsync(
        string baseRef,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseRef);
        var repositoryPath = GetRequiredRepositoryPath();
        var normalizedBaseRef = baseRef.Trim();

        await ExecuteAsync(
            async () =>
            {
                BaseRef = null;
                SelectedDiffText = null;
                ClearChanges();

                var changes = await _repositoryService.GetChangesSinceAsync(
                    repositoryPath,
                    new GitComparisonRequest(normalizedBaseRef),
                    cancellationToken);

                BaseRef = normalizedBaseRef;
                SetChanges(changes);
            },
            cancellationToken);
    }

    public async Task LoadDiffAsync(
        ChangeItemViewModel item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var repositoryPath = GetRequiredRepositoryPath();

        await ExecuteAsync(
            async () =>
            {
                SelectedDiffText = null;
                SelectedDiffText = await _repositoryService.GetDiffAsync(
                    repositoryPath,
                    item.Path,
                    BaseRef,
                    MaxTextPreviewBytes,
                    cancellationToken);
            },
            cancellationToken);
    }

    private async Task ExecuteAsync(
        Func<Task> operation,
        CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await operation();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearRepositoryState()
    {
        RepositoryPath = null;
        BranchName = null;
        HeadSha = null;
        BaseRef = null;
        SelectedDiffText = null;
        ClearChanges();
    }

    private void SetWorkingTreeChanges(IReadOnlyList<GitWorkingTreeChange> changes)
    {
        var domainChanges = changes
            .Select(static change => new GitChange(change.Path, change.Kind, change.OriginalPath))
            .ToArray();
        var flagsByPath = changes.ToDictionary(
            static change => change.Path,
            static change => (change.IsStaged, change.IsUnstaged),
            StringComparer.Ordinal);

        SetChanges(
            domainChanges,
            item => flagsByPath.TryGetValue(item.Path, out var flags)
                ? flags
                : default);
    }

    private void SetChanges(
        IReadOnlyList<GitChange> changes,
        Func<ReviewItem, (bool IsStaged, bool IsUnstaged)>? flagsProvider = null)
    {
        _reviewSession = ReviewSession.Create(changes);
        Changes.Clear();

        foreach (var item in _reviewSession.Items)
        {
            var flags = flagsProvider?.Invoke(item) ?? default;
            Changes.Add(new ChangeItemViewModel(
                _reviewSession,
                item,
                flags.IsStaged,
                flags.IsUnstaged));
        }
    }

    private void ClearChanges()
    {
        _reviewSession = null;
        Changes.Clear();
    }

    private string GetRequiredRepositoryPath() =>
        !string.IsNullOrWhiteSpace(RepositoryPath)
            ? RepositoryPath
            : throw new InvalidOperationException("Open a Git repository before running this operation.");
}
