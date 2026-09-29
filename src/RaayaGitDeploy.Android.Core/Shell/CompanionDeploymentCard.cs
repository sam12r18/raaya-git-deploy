namespace RaayaGitDeploy.Android.Core.Shell;

/// <summary>
/// Render-ready deployment card for the Android host. It intentionally contains only
/// companion workflow state and never transport endpoints or raw credentials.
/// </summary>
public sealed record CompanionDeploymentCard(
    string Title,
    string StatusText,
    string? Detail,
    CompanionDeploymentCardTone Tone,
    bool ShowProgress,
    bool CanRefresh,
    bool CanRetry,
    TimeSpan? RetryAfter)
{
    public static CompanionDeploymentCard From(CompanionDeploymentPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        if (presentation.RetryAfter is { } retryAfter)
            return new("Deployment paused", "Rate limited", presentation.ActivityMessage, CompanionDeploymentCardTone.Warning, false, false, true, retryAfter);

        if (presentation.IsRunning)
            return new("Deploying", presentation.State ?? "Running", presentation.ActivityMessage, CompanionDeploymentCardTone.Progress, true, presentation.CanRefresh, false, null);

        if (presentation.IsTerminal && !string.IsNullOrWhiteSpace(presentation.FailureMessage))
            return new("Deployment failed", presentation.State ?? "Failed", presentation.FailureMessage, CompanionDeploymentCardTone.Error, false, false, true, null);

        if (presentation.IsTerminal)
            return new("Deployment complete", presentation.State ?? "Succeeded", presentation.ActivityMessage, CompanionDeploymentCardTone.Success, false, false, false, null);

        return new("Deployment", presentation.State ?? "Ready", presentation.ActivityMessage, CompanionDeploymentCardTone.Neutral, false, presentation.CanRefresh, false, null);
    }
}

public enum CompanionDeploymentCardTone
{
    Neutral,
    Progress,
    Success,
    Warning,
    Error
}
