using Android.App;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;
using RaayaGitDeploy.Android.Core.Api;
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
    private EditText? _agentEndpoint;
    private EditText? _accessToken;
    private Button? _signInButton;
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
        _agentEndpoint = new EditText(this)
        {
            Hint = "https://agent.example.com/"
        };
        _agentEndpoint.SetPadding(32, 8, 32, 8);
        _accessToken = new EditText(this)
        {
            Hint = "Companion API access token",
            InputType = InputTypes.ClassText | InputTypes.TextVariationPassword
        };
        _accessToken.SetPadding(32, 8, 32, 8);
        _signInButton = new Button(this) { Text = "Sign in to agent" };
        _signInButton.SetPadding(32, 8, 32, 8);
        _signInButton.Click += SignInButton_Click;
        _signOutButton = new Button(this) { Text = "Sign out" };
        _signOutButton.SetPadding(32, 8, 32, 8);
        _signOutButton.Click += SignOutButton_Click;
        _deploymentView = new DeploymentScreenView(this);
        root.AddView(_sessionStatus);
        root.AddView(_agentEndpoint);
        root.AddView(_accessToken);
        root.AddView(_signInButton);
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

    private async void SignInButton_Click(object? sender, EventArgs e)
    {
        if (_sessionLifecycle is null || _signInButton is null || _agentEndpoint is null || _accessToken is null)
            return;

        var endpointText = _agentEndpoint.Text?.Trim();
        var token = _accessToken.Text?.Trim();
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint) || string.IsNullOrWhiteSpace(token))
        {
            _sessionStatus!.Text = "Enter a valid HTTPS agent endpoint and companion API access token.";
            return;
        }

        _signInButton.Enabled = false;
        try
        {
            // Store through the Keystore-backed boundary first so the same token source is used by
            // the authenticated API client. Any validation failure immediately clears the session.
            await _sessionLifecycle.SignInAsync(token, CancellationToken.None);
            _accessToken.Text = string.Empty;

            var tokenStore = AndroidAccessTokenStoreFactory.Create(this);
            using var httpClient = new HttpClient { BaseAddress = endpoint };
            var api = new CompanionDeploymentHttpClient(httpClient, tokenStore);
            await api.GetRepositoriesAsync(CancellationToken.None);

            RenderSessionState(true);
            RenderDeploymentState(true);
        }
        catch (Exception ex)
        {
            await _sessionLifecycle.SignOutAsync(CancellationToken.None);
            RenderSessionState(false);
            RenderDeploymentState(false);
            _sessionStatus!.Text = $"Agent sign-in failed: {ex.Message}";
        }
        finally
        {
            _signInButton.Enabled = true;
        }
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
        if (_sessionStatus is null || _signOutButton is null || _signInButton is null || _agentEndpoint is null || _accessToken is null)
            return;

        _sessionStatus.Text = hasProtectedSession
            ? "Authorized companion session is protected by Android Keystore."
            : "No authorized companion session. Sign in to the agent before deployment actions are enabled.";
        _signOutButton.Visibility = hasProtectedSession ? ViewStates.Visible : ViewStates.Gone;
        _signInButton.Visibility = hasProtectedSession ? ViewStates.Gone : ViewStates.Visible;
        _agentEndpoint.Visibility = hasProtectedSession ? ViewStates.Gone : ViewStates.Visible;
        _accessToken.Visibility = hasProtectedSession ? ViewStates.Gone : ViewStates.Visible;
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
