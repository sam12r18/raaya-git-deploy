namespace RaayaGitDeploy.Android.Core.Shell;

/// <summary>
/// Single render contract for the Android deployment screen. The platform host can bind
/// this state without receiving transport endpoints or raw Git/SSH/FTP credentials.
/// </summary>
public sealed record CompanionDeploymentScreenState(
    CompanionDeploymentCard Card,
    CompanionDeploymentScreenAction PrimaryAction,
    string? PrimaryActionText,
    bool PrimaryActionEnabled,
    CompanionDeploymentScreenAction SecondaryAction,
    string? SecondaryActionText,
    bool SecondaryActionEnabled)
{
    public static CompanionDeploymentScreenState From(CompanionDeploymentPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        var card = CompanionDeploymentCard.From(presentation);

        if (card.CanRetry)
            return new(card, CompanionDeploymentScreenAction.Retry, "Retry", true, CompanionDeploymentScreenAction.None, null, false);

        if (card.CanRefresh)
            return new(card, CompanionDeploymentScreenAction.Refresh, "Refresh", true, CompanionDeploymentScreenAction.None, null, false);

        return new(card, CompanionDeploymentScreenAction.None, null, false, CompanionDeploymentScreenAction.None, null, false);
    }
}

/// <summary>
/// Typed, allow-listed actions that an Android platform host may dispatch back to the
/// companion workflow. No arbitrary command or transport operation can be encoded here.
/// </summary>
public enum CompanionDeploymentScreenAction
{
    None,
    Refresh,
    Retry
}
