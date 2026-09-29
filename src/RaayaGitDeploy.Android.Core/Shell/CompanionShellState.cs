using RaayaGitDeploy.Android.Core.Api;
using RaayaGitDeploy.Android.Core.Deployment;
using RaayaGitDeploy.Core.Companion;

namespace RaayaGitDeploy.Android.Core.Shell;

/// <summary>
/// Platform-neutral navigation state for the Android companion shell. Android UI renders these safe
/// workflow stages; it never receives SSH keys, host passwords, or raw Git credentials.
/// </summary>
public sealed class CompanionShellState
{
    private readonly CompanionDeploymentWorkflow _workflow;

    public CompanionShellState(CompanionDeploymentWorkflow workflow) => _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));

    public CompanionShellScreen Screen { get; private set; } = CompanionShellScreen.Repositories;
    public bool IsBusy { get; private set; }
    public string? LastError { get; private set; }
    public TimeSpan? RetryAfter { get; private set; }
    public bool CanRetry => !IsBusy && LastError is not null;
    public bool CanOpenProfiles => _workflow.SelectedRepository is not null;
    public bool CanOpenHistory => _workflow.SelectedRepository is not null;
    public bool CanOpenDeployment => _workflow.SelectedRepository is not null && _workflow.SelectedProfile is not null;
    public CompanionDeploymentActivityState DeploymentActivityState => _workflow.ActivityState;
    public string? DeploymentActivityMessage => _workflow.ActivityMessage;
    public string? CurrentDeploymentId => _workflow.CurrentRun?.Id;
    public string? CurrentDeploymentState => _workflow.CurrentRun?.State;
    public string? CurrentDeploymentFailureMessage => _workflow.CurrentRun?.FailureMessage;
    public DateTimeOffset? CurrentDeploymentStartedAt => _workflow.CurrentRun?.StartedAt;
    public DateTimeOffset? CurrentDeploymentFinishedAt => _workflow.CurrentRun?.FinishedAt;
    public bool HasDeploymentFailure => !string.IsNullOrWhiteSpace(_workflow.CurrentRun?.FailureMessage);
    public bool HasDeploymentResult => _workflow.CurrentRun is not null;
    public bool IsDeploymentRunning => _workflow.CurrentRun is not null && !_workflow.IsCurrentRunTerminal;
    public bool IsDeploymentTerminal => _workflow.IsCurrentRunTerminal;
    public bool CanRefreshDeployment => !IsBusy && IsDeploymentRunning;
    public bool CanStartDeployment => !IsBusy && CanOpenDeployment && _workflow.Preview is not null && _workflow.ActivityState == CompanionDeploymentActivityState.Ready;

    public void OpenRepositories() => Screen = CompanionShellScreen.Repositories;
    public void OpenProfiles() { if (!CanOpenProfiles) throw new InvalidOperationException("Select an authorized repository before opening deployment profiles."); Screen = CompanionShellScreen.Profiles; }
    public void OpenHistory() { if (!CanOpenHistory) throw new InvalidOperationException("Select an authorized repository before opening deployment history."); Screen = CompanionShellScreen.History; }

    public async Task OpenHistoryDetailAsync(string deploymentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deploymentId) || !_workflow.History.Any(run => string.Equals(run.Id, deploymentId, StringComparison.Ordinal))) throw new InvalidOperationException("Select a deployment from the loaded repository history.");
        var previousScreen = Screen;
        await RunBusyAsync(() => _workflow.LoadHistoryDetailAsync(deploymentId, cancellationToken), cancellationToken, onSuccess: () => Screen = CompanionShellScreen.HistoryDetail, onFailure: () => Screen = previousScreen);
    }

    public Task<CompanionDeploymentPreview> DryRunAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken) => RunBusyAsync(() => _workflow.DryRunAsync(paths, cancellationToken), cancellationToken);
    public Task<CompanionDeploymentRun> StartDeploymentAsync(bool confirmed, CancellationToken cancellationToken) => RunBusyAsync(() => _workflow.StartDeploymentAsync(confirmed, cancellationToken), cancellationToken);
    public Task<CompanionDeploymentRun> RefreshDeploymentAsync(CancellationToken cancellationToken) => RunBusyAsync(() => _workflow.RefreshDeploymentAsync(cancellationToken), cancellationToken);

    public Task<CompanionDeploymentRun> PollDeploymentUntilTerminalAsync(int maxAttempts, TimeSpan delay, CancellationToken cancellationToken)
    {
        if (!CanRefreshDeployment) throw new InvalidOperationException("Start a non-terminal deployment before polling progress.");
        return RunBusyAsync(() => _workflow.PollUntilTerminalAsync(maxAttempts, delay, cancellationToken), cancellationToken);
    }

    public void ClearError() { LastError = null; RetryAfter = null; }
    public void OpenDeployment() { if (!CanOpenDeployment) throw new InvalidOperationException("Select an authorized repository and deployment profile first."); Screen = CompanionShellScreen.Deployment; }

    private async Task RunBusyAsync(Func<Task> operation, CancellationToken cancellationToken, Action? onSuccess = null, Action? onFailure = null)
    {
        if (IsBusy) throw new InvalidOperationException("Another companion operation is already in progress."); ClearError(); IsBusy = true;
        try { await operation().ConfigureAwait(false); onSuccess?.Invoke(); }
        catch (OperationCanceledException) { onFailure?.Invoke(); throw; }
        catch (CompanionRateLimitedException exception) { onFailure?.Invoke(); LastError = exception.Message; RetryAfter = exception.RetryAfter; throw; }
        catch (Exception exception) { onFailure?.Invoke(); LastError = exception.Message; throw; }
        finally { IsBusy = false; }
    }

    private async Task<T> RunBusyAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken, Action? onSuccess = null, Action? onFailure = null)
    {
        if (IsBusy) throw new InvalidOperationException("Another companion operation is already in progress."); ClearError(); IsBusy = true;
        try { var result = await operation().ConfigureAwait(false); onSuccess?.Invoke(); return result; }
        catch (OperationCanceledException) { onFailure?.Invoke(); throw; }
        catch (CompanionRateLimitedException exception) { onFailure?.Invoke(); LastError = exception.Message; RetryAfter = exception.RetryAfter; throw; }
        catch (Exception exception) { onFailure?.Invoke(); LastError = exception.Message; throw; }
        finally { IsBusy = false; }
    }
}

public enum CompanionShellScreen { Repositories, Profiles, Deployment, History, HistoryDetail }
