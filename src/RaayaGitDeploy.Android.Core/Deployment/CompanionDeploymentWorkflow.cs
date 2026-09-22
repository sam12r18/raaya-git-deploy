using RaayaGitDeploy.Core.Companion;

namespace RaayaGitDeploy.Android.Core.Deployment;

/// <summary>
/// Mobile-safe deployment workflow. The companion only works with agent-issued repository/profile IDs
/// and preview IDs; host credentials and destructive transport details never cross this boundary.
/// </summary>
public sealed class CompanionDeploymentWorkflow
{
    private static readonly HashSet<string> TerminalStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "succeeded", "failed", "cancelled", "canceled", "blocked"
    };

    private readonly ICompanionDeploymentApi _api;

    public CompanionDeploymentWorkflow(ICompanionDeploymentApi api) =>
        _api = api ?? throw new ArgumentNullException(nameof(api));

    public CompanionRepository? SelectedRepository { get; private set; }
    public CompanionDeploymentProfile? SelectedProfile { get; private set; }
    public IReadOnlyList<CompanionRepository> Repositories { get; private set; } = [];
    public IReadOnlyList<CompanionDeploymentProfile> Profiles { get; private set; } = [];
    public IReadOnlyList<CompanionDeploymentRun> History { get; private set; } = [];
    public CompanionDeploymentPreview? Preview { get; private set; }
    public CompanionDeploymentRun? CurrentRun { get; private set; }
    public CompanionDeploymentRun? SelectedHistoryRun { get; private set; }
    public bool IsCurrentRunTerminal => CurrentRun is not null && TerminalStates.Contains(CurrentRun.State);
    public CompanionDeploymentActivityState ActivityState { get; private set; } = CompanionDeploymentActivityState.Idle;
    public string? ActivityMessage { get; private set; }

    public async Task LoadRepositoriesAsync(CancellationToken cancellationToken)
    {
        Repositories = await _api.GetRepositoriesAsync(cancellationToken).ConfigureAwait(false);
        SelectedRepository = null;
        SelectedProfile = null;
        Profiles = [];
        History = [];
        Preview = null;
        CurrentRun = null;
        SelectedHistoryRun = null;
        SetActivity(CompanionDeploymentActivityState.Idle);
    }

    public async Task SelectRepositoryAsync(string repositoryId, CancellationToken cancellationToken)
    {
        SelectedRepository = Repositories.FirstOrDefault(item => item.Id == repositoryId)
            ?? throw new InvalidOperationException("Select an authorized repository returned by the companion agent.");
        Profiles = await _api.GetProfilesAsync(SelectedRepository.Id, cancellationToken).ConfigureAwait(false);
        SelectedProfile = null;
        History = [];
        Preview = null;
        CurrentRun = null;
        SelectedHistoryRun = null;
        SetActivity(CompanionDeploymentActivityState.Idle);
    }

    public void SelectProfile(string profileId)
    {
        SelectedProfile = Profiles.FirstOrDefault(item => item.Id == profileId)
            ?? throw new InvalidOperationException("Select an authorized deployment profile returned by the companion agent.");
        Preview = null;
        CurrentRun = null;
        SetActivity(CompanionDeploymentActivityState.Idle);
    }

    public async Task<IReadOnlyList<CompanionDeploymentRun>> LoadHistoryAsync(CancellationToken cancellationToken)
    {
        var repository = SelectedRepository ?? throw new InvalidOperationException("Select a repository before loading deployment history.");
        History = [];
        SelectedHistoryRun = null;
        var history = await _api.GetDeploymentHistoryAsync(repository.Id, cancellationToken).ConfigureAwait(false);

        if (history.Any(run => !string.Equals(run.RepositoryId, repository.Id, StringComparison.Ordinal)))
            throw new InvalidOperationException("The companion agent returned deployment history for another repository.");

        History = history;
        return History;
    }

    public async Task<CompanionDeploymentRun> LoadHistoryDetailAsync(string deploymentId, CancellationToken cancellationToken)
    {
        var repository = SelectedRepository ?? throw new InvalidOperationException("Select a repository before loading deployment details.");
        if (string.IsNullOrWhiteSpace(deploymentId) || !History.Any(run => string.Equals(run.Id, deploymentId, StringComparison.Ordinal)))
            throw new InvalidOperationException("Select a deployment from the loaded repository history.");

        SelectedHistoryRun = null;
        var detail = await _api.GetDeploymentAsync(deploymentId, cancellationToken).ConfigureAwait(false);
        EnsureRunScope(detail, repository.Id, expectedProfileId: null);

        SelectedHistoryRun = detail;
        return detail;
    }

    public async Task<CompanionDeploymentPreview> DryRunAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var repository = SelectedRepository ?? throw new InvalidOperationException("Select a repository before Dry Run.");
        var profile = SelectedProfile ?? throw new InvalidOperationException("Select a deployment profile before Dry Run.");
        if (paths.Count == 0 || paths.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Select at least one valid repository path before Dry Run.");

        Preview = null;
        CurrentRun = null;
        SetActivity(CompanionDeploymentActivityState.Loading, "Preparing Dry Run...");
        try
        {
            var preview = await _api.DryRunAsync(new CompanionDeploymentRequest(repository.Id, profile.Id, paths, DryRun: true), cancellationToken).ConfigureAwait(false);
            EnsurePreviewScope(preview, repository.Id, profile.Id);
            Preview = preview;
            SetActivity(CompanionDeploymentActivityState.Ready, "Dry Run ready for review.");
            return preview;
        }
        catch (OperationCanceledException)
        {
            SetActivity(CompanionDeploymentActivityState.Idle);
            throw;
        }
        catch (Exception ex)
        {
            SetActivity(CompanionDeploymentActivityState.RecoverableError, ex.Message);
            throw;
        }
    }

    public async Task<CompanionDeploymentRun> StartDeploymentAsync(bool confirmed, CancellationToken cancellationToken)
    {
        var preview = Preview ?? throw new InvalidOperationException("Run and review Dry Run before deployment.");
        var repository = SelectedRepository ?? throw new InvalidOperationException("Select a repository before deployment.");
        var profile = SelectedProfile ?? throw new InvalidOperationException("Select a deployment profile before deployment.");
        if (profile.RequiresConfirmation && !confirmed)
            throw new InvalidOperationException("This deployment profile requires explicit confirmation.");

        SetActivity(CompanionDeploymentActivityState.Loading, "Starting deployment...");
        try
        {
            var run = await _api.StartDeploymentAsync(preview.Id, confirmed, cancellationToken).ConfigureAwait(false);
            EnsureRunScope(run, repository.Id, profile.Id);
            CurrentRun = run;
            UpdateActivityFromRun(run);
            return run;
        }
        catch (OperationCanceledException)
        {
            SetActivity(CompanionDeploymentActivityState.Ready, "Deployment start cancelled; Dry Run remains available.");
            throw;
        }
        catch (Exception ex)
        {
            SetActivity(CompanionDeploymentActivityState.RecoverableError, ex.Message);
            throw;
        }
    }

    public async Task<CompanionDeploymentRun> RefreshDeploymentAsync(CancellationToken cancellationToken)
    {
        var run = CurrentRun ?? throw new InvalidOperationException("Start a deployment before requesting progress.");
        var repository = SelectedRepository ?? throw new InvalidOperationException("Select a repository before requesting progress.");
        var profile = SelectedProfile ?? throw new InvalidOperationException("Select a deployment profile before requesting progress.");
        if (IsCurrentRunTerminal)
        {
            UpdateActivityFromRun(run);
            return run;
        }

        try
        {
            var refreshed = await _api.GetDeploymentAsync(run.Id, cancellationToken).ConfigureAwait(false);
            EnsureRunScope(refreshed, repository.Id, profile.Id);
            CurrentRun = refreshed;
            UpdateActivityFromRun(refreshed);
            return refreshed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetActivity(CompanionDeploymentActivityState.RecoverableError, ex.Message);
            throw;
        }
    }

    public async Task<CompanionDeploymentRun> PollUntilTerminalAsync(int maxAttempts, TimeSpan delay, CancellationToken cancellationToken)
    {
        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Polling requires at least one attempt.");
        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay), "Polling delay cannot be negative.");
        if (CurrentRun is null)
            throw new InvalidOperationException("Start a deployment before polling progress.");

        for (var attempt = 0; attempt < maxAttempts && !IsCurrentRunTerminal; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RefreshDeploymentAsync(cancellationToken).ConfigureAwait(false);
            if (!IsCurrentRunTerminal && attempt + 1 < maxAttempts && delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        return CurrentRun!;
    }

    private void UpdateActivityFromRun(CompanionDeploymentRun run)
    {
        SetActivity(TerminalStates.Contains(run.State) ? CompanionDeploymentActivityState.Terminal : CompanionDeploymentActivityState.Running, run.State);
    }

    private void SetActivity(CompanionDeploymentActivityState state, string? message = null)
    {
        ActivityState = state;
        ActivityMessage = message;
    }

    private static void EnsurePreviewScope(CompanionDeploymentPreview preview, string repositoryId, string profileId)
    {
        if (!string.Equals(preview.RepositoryId, repositoryId, StringComparison.Ordinal) ||
            !string.Equals(preview.ProfileId, profileId, StringComparison.Ordinal))
            throw new InvalidOperationException("The companion agent returned a Dry Run preview outside the selected repository or profile scope.");
    }

    private static void EnsureRunScope(CompanionDeploymentRun run, string repositoryId, string? expectedProfileId)
    {
        if (!string.Equals(run.RepositoryId, repositoryId, StringComparison.Ordinal))
            throw new InvalidOperationException("The companion agent returned a deployment for another repository.");
        if (expectedProfileId is not null && !string.Equals(run.ProfileId, expectedProfileId, StringComparison.Ordinal))
            throw new InvalidOperationException("The companion agent returned a deployment for another profile.");
    }
}

public enum CompanionDeploymentActivityState
{
    Idle,
    Loading,
    Ready,
    Running,
    RecoverableError,
    Terminal
}
