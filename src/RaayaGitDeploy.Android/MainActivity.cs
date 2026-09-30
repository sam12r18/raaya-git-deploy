using Android.App;
using Android.OS;
using Android.Widget;
using RaayaGitDeploy.Android.Core.Security;
using RaayaGitDeploy.Android.Core.Shell;
using RaayaGitDeploy.Android.Security;

namespace RaayaGitDeploy.Android;

[Activity(Label = "Raaya Git Deploy", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    private DeploymentScreenView? _deploymentView;
    private CompanionSessionLifecycle? _sessionLifecycle;

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _deploymentView = new DeploymentScreenView(this);
        SetContentView(_deploymentView);

        var accessTokenStore = AndroidAccessTokenStoreFactory.Create(this);
        _sessionLifecycle = new CompanionSessionLifecycle(accessTokenStore);
        var hasProtectedSession = false;
        try
        {
            hasProtectedSession = await _sessionLifecycle.RestoreAsync(CancellationToken.None);
        }
        catch
        {
            // A missing/invalidated Keystore key must degrade to re-authentication, never to
            // plaintext persistence or a less secure credential path.
            await _sessionLifecycle.SignOutAsync(CancellationToken.None);
        }

        var initial = new CompanionDeploymentScreenState(
            new CompanionDeploymentCard(
                "Deployment",
                hasProtectedSession ? "Session protected" : "Ready",
                hasProtectedSession
                    ? "A protected companion session is available. Connect to the authorized agent to refresh deployment state."
                    : "Connect to the authorized companion agent to review a deployment.",
                CompanionDeploymentCardTone.Neutral,
                false, false, false, null),
            CompanionDeploymentScreenAction.None, null, false,
            CompanionDeploymentScreenAction.None, null, false);
        _deploymentView.Render(initial, _ => Task.CompletedTask);
    }
}
