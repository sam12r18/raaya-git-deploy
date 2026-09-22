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
    }

    public void SelectProfile(string profileId)
    {
        SelectedProfile = Profiles.FirstOrDefault(item => item.Id == profileId)
            ?? throw new InvalidOperationException("Select an authorized deployment profile returned by the companion agent.");
        Preview = null;
        CurrentRun = null;
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
        EnsureRunScope(detail, repository.Id);

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
        var preview = await _api.DryRunAsync(new CompanionDeploymentRequest(repository.Id, profile.Id, paths, DryRun: true), cancellationToken).ConfigureAwait(false);
        EnsurePreviewScope(preview, repository.Id, profile.Id);
        Preview = preview;
        return preview;
    }

    public async Task<CompanionDeploymentRun> StartDeploymentAsync(bool confirmed, CancellationToken cancellationToken)
    {
        var preview = Preview ?? throw new InvalidOperationException("Run and review Dry Run before deployment.");
        var repository = SelectedRepository ?? throw new InvalidOperationException("Select a repository before deployment.");
        var profile = SelectedProfile ?? throw new InvalidOperationException("Select a deployment profile before deployment.");
        if (profile.RequiresConfirmation && !confirmed)
            throw new InvalidOperationException("This deployment profile requires explicit confirmation.");

        var run = await _api.StartDeploymentAsync(preview.Id, confirmed, cancellationToken).ConfigureAwait(false);
        EnsureRunScope(run, repository.Id);
        CurrentRun = run;
        return run;
    }

    public async Task<CompanionDeploymentRun> RefreshDeploymentAsync(CancellationToken cancellationToken)
    {
        var run = CurrentRun ?? throw new InvalidOperationException("Start a deployment before requesting progress.");
        var repository = SelectedRepository ?? throw new InvalidOperationException("Select a repository before requesting progress.");
        if (IsCurrentRunTerminal)
            return run;

        var refreshed = await _api.GetDeploymentAsync(run.Id, cancellationToken).ConfigureAwait(false);
        EnsureRunScope(refreshed, repository.Id);
        CurrentRun = refreshed;
        return refreshed;
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

    private static void EnsurePreviewScope(CompanionDeploymentPreview preview, string repositoryId, string profileId)
    {
        if (!string.Equals(preview.RepositoryId, repositoryId, StringComparison.Ordinal) ||
            !string.Equals(preview.ProfileId, profileId, StringComparison.Ordinal))
            throw new InvalidOperationException("The companion agent returned a Dry Run preview outside the selected repository or profile scope.");
    }

    private static void EnsureRunScope(CompanionDeploymentRun run, string repositoryId)
    {
        if (!string.Equals(run.RepositoryId, repositoryId, StringComparison.Ordinal))
            throw new InvalidOperationException("The companion agent returned a deployment for another repository.");
    }
}
