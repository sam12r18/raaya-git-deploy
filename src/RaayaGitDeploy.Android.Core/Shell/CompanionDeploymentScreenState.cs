namespace RaayaGitDeploy.Android.Core.Shell;

/// <summary>
/// Single render contract for the Android deployment screen. The platform host can bind
/// this state without receiving transport endpoints or raw Git/SSH/FTP credentials.
/// </summary>
public sealed record CompanionDeploymentScreenState(
    CompanionDeploymentCard Card,
    string? PrimaryActionText,
    bool PrimaryActionEnabled,
    string? SecondaryActionText,
    bool SecondaryActionEnabled)
{
    public static CompanionDeploymentScreenState From(CompanionDeploymentPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        var card = CompanionDeploymentCard.From(presentation);

        if (card.CanRetry)
            return new(card, "Retry", true, null, false);

        if (card.CanRefresh)
            return new(card, "Refresh", true, null, false);

        return new(card, null, false, null, false);
    }
}
