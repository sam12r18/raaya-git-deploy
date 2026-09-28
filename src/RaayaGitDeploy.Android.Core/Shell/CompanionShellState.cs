using RaayaGitDeploy.Android.Core.Deployment;

namespace RaayaGitDeploy.Android.Core.Shell;

/// <summary>
/// Platform-neutral navigation state for the Android companion shell. Android UI renders these safe
/// workflow stages; it never receives SSH keys, host passwords, or raw Git credentials.
/// </summary>
public sealed class CompanionShellState
{
    private readonly CompanionDeploymentWorkflow _workflow;

    public CompanionShellState(CompanionDeploymentWorkflow workflow) =>
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));

    public CompanionShellScreen Screen { get; private set; } = CompanionShellScreen.Repositories;
    public bool IsBusy { get; private set; }
    public string? LastError { get; private set; }
    public bool CanRetry => !IsBusy && LastError is not null;
    public bool CanOpenProfiles => _workflow.SelectedRepository is not null;
    public bool CanOpenHistory => _workflow.SelectedRepository is not null;
    public bool CanOpenDeployment => _workflow.SelectedRepository is not null && _workflow.SelectedProfile is not null;

    public void OpenRepositories() => Screen = CompanionShellScreen.Repositories;

    public void OpenProfiles()
    {
        if (!CanOpenProfiles)
            throw new InvalidOperationException("Select an authorized repository before opening deployment profiles.");
        Screen = CompanionShellScreen.Profiles;
    }

    public void OpenHistory()
    {
        if (!CanOpenHistory)
            throw new InvalidOperationException("Select an authorized repository before opening deployment history.");
        Screen = CompanionShellScreen.History;
    }

    public async Task OpenHistoryDetailAsync(string deploymentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deploymentId) || !_workflow.History.Any(run => string.Equals(run.Id, deploymentId, StringComparison.Ordinal)))
            throw new InvalidOperationException("Select a deployment from the loaded repository history.");

        var previousScreen = Screen;
        LastError = null;
        IsBusy = true;
        try
        {
            await _workflow.LoadHistoryDetailAsync(deploymentId, cancellationToken);
            Screen = CompanionShellScreen.HistoryDetail;
        }
        catch (OperationCanceledException)
        {
            Screen = previousScreen;
            throw;
        }
        catch (Exception exception)
        {
            Screen = previousScreen;
            LastError = exception.Message;
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ClearError() => LastError = null;

    public void OpenDeployment()
    {
        if (!CanOpenDeployment)
            throw new InvalidOperationException("Select an authorized repository and deployment profile first.");
        Screen = CompanionShellScreen.Deployment;
    }
}

public enum CompanionShellScreen
{
    Repositories,
    Profiles,
    Deployment,
    History,
    HistoryDetail
}
