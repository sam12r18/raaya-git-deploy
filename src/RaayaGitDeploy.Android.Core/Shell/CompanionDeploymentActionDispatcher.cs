namespace RaayaGitDeploy.Android.Core.Shell;

/// <summary>
/// Narrow command boundary for the Android host. Only allow-listed screen actions can cross
/// into the companion workflow; a failed deployment retry is deliberately not auto-executed
/// because starting a deployment requires a fresh explicit confirmation.
/// </summary>
public sealed class CompanionDeploymentActionDispatcher
{
    private readonly CompanionShellState _shell;

    public CompanionDeploymentActionDispatcher(CompanionShellState shell) =>
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));

    public async Task<CompanionDeploymentActionResult> DispatchAsync(
        CompanionDeploymentScreenAction action,
        CancellationToken cancellationToken)
    {
        switch (action)
        {
            case CompanionDeploymentScreenAction.None:
                return CompanionDeploymentActionResult.NoOp;

            case CompanionDeploymentScreenAction.Refresh:
                if (!_shell.CanRefreshDeployment)
                    throw new InvalidOperationException("The current deployment cannot be refreshed.");
                await _shell.RefreshDeploymentAsync(cancellationToken).ConfigureAwait(false);
                return CompanionDeploymentActionResult.Completed;

            case CompanionDeploymentScreenAction.Retry:
                // Never turn a UI retry tap into an implicit deployment. The host must return
                // to the reviewed deployment flow and obtain explicit confirmation again.
                return CompanionDeploymentActionResult.RequiresDeploymentConfirmation;

            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported companion deployment action.");
        }
    }
}

public enum CompanionDeploymentActionResult
{
    NoOp,
    Completed,
    RequiresDeploymentConfirmation
}
