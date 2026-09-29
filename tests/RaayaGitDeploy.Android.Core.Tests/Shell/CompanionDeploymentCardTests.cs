using RaayaGitDeploy.Android.Core.Shell;

namespace RaayaGitDeploy.Android.Core.Tests.Shell;

public sealed class CompanionDeploymentCardTests
{
    [Fact]
    public void Running_presentation_maps_to_progress_card()
    {
        var presentation = new CompanionDeploymentPresentation("run-1", "running", "Uploading files", null, DateTimeOffset.UtcNow, null, true, false, true, null);

        var card = CompanionDeploymentCard.From(presentation);

        Assert.Equal(CompanionDeploymentCardTone.Progress, card.Tone);
        Assert.True(card.ShowProgress);
        Assert.True(card.CanRefresh);
        Assert.False(card.CanRetry);
    }

    [Fact]
    public void Failed_terminal_presentation_maps_to_retryable_error_card()
    {
        var now = DateTimeOffset.UtcNow;
        var presentation = new CompanionDeploymentPresentation("run-2", "failed", null, "Upload failed", now.AddMinutes(-1), now, false, true, false, null);

        var card = CompanionDeploymentCard.From(presentation);

        Assert.Equal(CompanionDeploymentCardTone.Error, card.Tone);
        Assert.Equal("Upload failed", card.Detail);
        Assert.True(card.CanRetry);
        Assert.False(card.ShowProgress);
    }

    [Fact]
    public void Rate_limited_presentation_preserves_retry_after_without_transport_details()
    {
        var retryAfter = TimeSpan.FromSeconds(30);
        var presentation = new CompanionDeploymentPresentation("run-3", "running", "Try again later", null, DateTimeOffset.UtcNow, null, true, false, false, retryAfter);

        var card = CompanionDeploymentCard.From(presentation);

        Assert.Equal(CompanionDeploymentCardTone.Warning, card.Tone);
        Assert.Equal(retryAfter, card.RetryAfter);
        Assert.True(card.CanRetry);
        Assert.False(card.CanRefresh);
    }
}
