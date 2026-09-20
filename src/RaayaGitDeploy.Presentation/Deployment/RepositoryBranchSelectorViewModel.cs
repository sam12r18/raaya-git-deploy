using CommunityToolkit.Mvvm.ComponentModel;
using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Presentation.Deployment;

public enum RepositoryTrackingState
{
    Idle,
    Fetching,
    Success,
    Error
}

public sealed partial class RepositoryBranchSelectorViewModel : ObservableObject
{
    private readonly IGitRepositoryService _repositories;
    private readonly IGitRemoteTrackingService _remoteTracking;

    [ObservableProperty] private string repositoryPath = string.Empty;
    [ObservableProperty] private string remoteName = "origin";
    [ObservableProperty] private string branchName = "main";
    [ObservableProperty] private string localRevision = string.Empty;
    [ObservableProperty] private string remoteRevision = string.Empty;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private RepositoryTrackingState state = RepositoryTrackingState.Idle;

    public RepositoryBranchSelectorViewModel(
        IGitRepositoryService repositories,
        IGitRemoteTrackingService remoteTracking)
    {
        _repositories = repositories;
        _remoteTracking = remoteTracking;
    }

    public bool IsBusy => State == RepositoryTrackingState.Fetching;
    public bool HasRevisionDifference =>
        State == RepositoryTrackingState.Success &&
        !string.Equals(LocalRevision, RemoteRevision, StringComparison.OrdinalIgnoreCase);

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        LocalRevision = string.Empty;
        RemoteRevision = string.Empty;

        if (string.IsNullOrWhiteSpace(RepositoryPath))
        {
            SetError("Repository path is required.");
            return;
        }

        if (!IsSafeGitName(RemoteName))
        {
            SetError("Remote name is invalid.");
            return;
        }

        if (!IsSafeGitName(BranchName))
        {
            SetError("Branch name is invalid.");
            return;
        }

        State = RepositoryTrackingState.Fetching;
        NotifyDerivedState();

        try
        {
            var context = await _repositories.GetContextAsync(RepositoryPath.Trim(), cancellationToken);
            LocalRevision = context.HeadSha;

            await _remoteTracking.FetchAsync(
                context.RootPath,
                RemoteName.Trim(),
                cancellationToken);

            RemoteRevision = await _remoteTracking.GetRemoteRevisionAsync(
                context.RootPath,
                RemoteName.Trim(),
                BranchName.Trim(),
                cancellationToken);

            State = RepositoryTrackingState.Success;
        }
        catch (OperationCanceledException)
        {
            State = RepositoryTrackingState.Idle;
            throw;
        }
        catch (Exception exception)
        {
            SetError(exception.Message);
        }
        finally
        {
            NotifyDerivedState();
        }
    }

    private void SetError(string message)
    {
        ErrorMessage = message;
        State = RepositoryTrackingState.Error;
        NotifyDerivedState();
    }

    private void NotifyDerivedState()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(HasRevisionDifference));
    }

    private static bool IsSafeGitName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return !trimmed.StartsWith('-', StringComparison.Ordinal) &&
               !trimmed.Contains("..", StringComparison.Ordinal) &&
               !trimmed.Contains(' ') &&
               !trimmed.Contains('~') &&
               !trimmed.Contains('^') &&
               !trimmed.Contains(':') &&
               !trimmed.Contains('?') &&
               !trimmed.Contains('*') &&
               !trimmed.Contains('[') &&
               !trimmed.Contains('\\');
    }
}
