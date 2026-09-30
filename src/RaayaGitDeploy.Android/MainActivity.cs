using Android.App;
using Android.OS;
using Android.Views;
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
    private TextView? _sessionStatus;
    private Button? _signOutButton;

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var root = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        _sessionStatus = new TextView(this);
        _sessionStatus.SetPadding(32, 24, 32, 8);
        _signOutButton = new Button(this) { Text = "Sign out" };
        _signOutButton.SetPadding(32, 8, 32, 8);
        _signOutButton.Click += SignOutButton_Click;
        _deploymentView = new DeploymentScreenView(this);
        root.AddView(_sessionStatus);
        root.AddView(_signOutButton);
        root.AddView(_deploymentView, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));
        SetContentView(root);

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

        RenderSessionState(hasProtectedSession);
        RenderDeploymentState(hasProtectedSession);
    }

    private async void SignOutButton_Click(object? sender, EventArgs e)
    {
        if (_sessionLifecycle is null || _signOutButton is null) return;

        _signOutButton.Enabled = false;
        try
        {
            await _sessionLifecycle.SignOutAsync(CancellationToken.None);
            RenderSessionState(false);
            RenderDeploymentState(false);
        }
        finally
        {
            _signOutButton.Enabled = true;
        }
    }

    private void RenderSessionState(bool hasProtectedSession)
    {
        if (_sessionStatus is null || _signOutButton is null) return;
        _sessionStatus.Text = hasProtectedSession
            ? "Authorized companion session is protected by Android Keystore."
            : "No authorized companion session. Sign in to the agent before deployment actions are enabled.";
        _signOutButton.Visibility = hasProtectedSession ? ViewStates.Visible : ViewStates.Gone;
    }

    private void RenderDeploymentState(bool hasProtectedSession)
    {
        if (_deploymentView is null) return;
        var initial = new CompanionDeploymentScreenState(
            new CompanionDeploymentCard(
                "Deployment",
                hasProtectedSession ? "Session protected" : "Authentication required",
                hasProtectedSession
                    ? "Connect to the authorized agent to refresh deployment state."
                    : "Sign in to the authorized companion agent to review or execute a deployment.",
                CompanionDeploymentCardTone.Neutral,
                false, false, false, null),
            CompanionDeploymentScreenAction.None, null, false,
            CompanionDeploymentScreenAction.None, null, false);
        _deploymentView.Render(initial, _ => Task.CompletedTask);
    }
}
