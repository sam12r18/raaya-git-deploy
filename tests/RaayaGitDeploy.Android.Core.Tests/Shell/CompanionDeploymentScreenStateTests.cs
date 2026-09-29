using RaayaGitDeploy.Android.Core.Shell;

namespace RaayaGitDeploy.Android.Core.Tests.Shell;

public sealed class CompanionDeploymentScreenStateTests
{
    [Fact]
    public void Running_deployment_exposes_refresh_action_without_transport_details()
    {
        var presentation = new CompanionDeploymentPresentation(
            "run-1", "running", "Uploading files", null,
            DateTimeOffset.UtcNow, null, true, false, true, null);

        var state = CompanionDeploymentScreenState.From(presentation);

        Assert.Equal("Deploying", state.Card.Title);
        Assert.True(state.Card.ShowProgress);
        Assert.Equal("Refresh", state.PrimaryActionText);
        Assert.True(state.PrimaryActionEnabled);
    }

    [Fact]
    public void Failed_deployment_exposes_retry_action()
    {
        var presentation = new CompanionDeploymentPresentation(
            "run-2", "failed", null, "Upload failed",
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow,
            false, true, false, null);

        var state = CompanionDeploymentScreenState.From(presentation);

        Assert.Equal("Deployment failed", state.Card.Title);
        Assert.Equal("Retry", state.PrimaryActionText);
        Assert.True(state.PrimaryActionEnabled);
    }
}
