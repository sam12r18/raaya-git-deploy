using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using RaayaGitDeploy.Android.Core.Api;
using RaayaGitDeploy.Android.Core.Deployment;
using RaayaGitDeploy.Android.Core.Security;
using RaayaGitDeploy.Android.Security;

namespace RaayaGitDeploy.Android;

[Activity(Label = "Raaya Git Deploy", MainLauncher = true)]
public sealed class MainActivity : Activity
{
    private CompanionSessionLifecycle? _sessionLifecycle;
    private CompanionDeploymentView? _deploymentView;
    private TextView? _sessionStatus;
    private TextView? _repositoriesStatus;
    private ListView? _repositoriesList;
    private EditText? _agentEndpoint;
    private EditText? _accessToken;
    private Button? _signInButton;
    private Button? _signOutButton;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var root = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical
        };
        root.SetPadding(32, 32, 32, 32);

        _sessionStatus = new TextView(this);
        root.AddView(_sessionStatus);

        _agentEndpoint = new EditText(this)
        {
            Hint = "https://agent.example.com"
        };
        root.AddView(_agentEndpoint);

        _accessToken = new EditText(this)
        {
            Hint = "Companion API access token",
            InputType = global::Android.Text.InputTypes.ClassText | global::Android.Text.InputTypes.TextVariationPassword
        };
        root.AddView(_accessToken);

        _signInButton = new Button(this) { Text = "Sign in" };
        _signInButton.Click += SignInButton_Click;
        root.AddView(_signInButton);

        _signOutButton = new Button(this) { Text = "Sign out" };
        _signOutButton.Click += SignOutButton_Click;
        root.AddView(_signOutButton);

        _repositoriesStatus = new TextView(this);
        root.AddView(_repositoriesStatus);

        _repositoriesList = new ListView(this);
        root.AddView(_repositoriesList, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            0,
            1));

        _deploymentView = new CompanionDeploymentView(this);
        root.AddView(_deploymentView);

        SetContentView(root);

        var tokenStore = AndroidAccessTokenStoreFactory.Create(this);
        _sessionLifecycle = new CompanionSessionLifecycle(tokenStore);
        _ = RestoreSessionAsync();
    }

    private async void SignInButton_Click(object? sender, EventArgs e)
    {
        if (_sessionLifecycle is null || _agentEndpoint is null || _accessToken is null)
            return;

        var endpointText = _agentEndpoint.Text?.Trim() ?? string.Empty;
        var token = _accessToken.Text ?? string.Empty;
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint) || string.IsNullOrWhiteSpace(token))
        {
            RenderSessionState(false);
            RenderRepositories([], "Enter a valid HTTPS agent endpoint and access token.");
            return;
        }

        try
        {
            await _sessionLifecycle.SignInAsync(token);
            using var http = new HttpClient();
            var client = new CompanionDeploymentHttpClient(http, endpoint, _sessionLifecycle.TokenStore);
            var repositories = await client.GetRepositoriesAsync();
            _accessToken.Text = string.Empty;
            RenderSessionState(true);
            RenderRepositories(repositories, repositories.Count == 0
                ? "Signed in. No repositories are authorized for this companion session."
                : $"Signed in. {repositories.Count} authorized repositories available.");
            RenderDeploymentState(true);
        }
        catch (Exception ex)
        {
            await _sessionLifecycle.SignOutAsync();
            RenderSessionState(false);
            RenderRepositories([], $"Sign-in validation failed: {ex.Message}");
            RenderDeploymentState(false);
        }
    }

    private async void SignOutButton_Click(object? sender, EventArgs e)
    {
        if (_sessionLifecycle is null) return;
        await _sessionLifecycle.SignOutAsync();
        RenderSessionState(false);
        RenderRepositories([], "Signed out. Repository data cleared from the companion surface.");
        RenderDeploymentState(false);
    }

    private async Task RestoreSessionAsync()
    {
        if (_sessionLifecycle is null) return;
        try
        {
            var hasSession = await _sessionLifecycle.RestoreAsync();
            RenderSessionState(hasSession);
            RenderRepositories([], hasSession
                ? "Protected session restored. Enter the agent endpoint to refresh authorized repositories."
                : "Sign in to load repositories authorized by the companion agent.");
            RenderDeploymentState(hasSession);
        }
        catch (ProtectedAccessTokenInvalidatedException)
        {
            await _sessionLifecycle.SignOutAsync();
            RenderSessionState(false);
            RenderRepositories([], "Protected session was invalidated by Android Keystore. Sign in again.");
            RenderDeploymentState(false);
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

    private void RenderRepositories(IReadOnlyList<CompanionRepository> repositories, string status)
    {
        if (_repositoriesStatus is null || _repositoriesList is null) return;

        _repositoriesStatus.Text = status;
        var rows = repositories
            .Select(repository => $"{repository.DisplayName}  •  {repository.Branch}  •  {repository.HeadSha[..Math.Min(8, repository.HeadSha.Length)]}{(repository.HasChanges ? "  •  changes" : string.Empty)}")
            .ToArray();
        _repositoriesList.Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleListItem1, rows);
        _repositoriesList.Visibility = rows.Length == 0 ? ViewStates.Gone : ViewStates.Visible;
    }

    private void RenderDeploymentState(bool hasProtectedSession)
    {
        if (_deploymentView is null) return;
        var initial = new CompanionDeploymentScreenState(
            new CompanionDeploymentCard(
                "Deployment",
                hasProtectedSession ? "Session protected" : "Authentication required",
                hasProtectedSession
                    ? "Select an authorized repository and profile before requesting a Dry Run."
                    : "Sign in to the authorized companion agent to review or execute a deployment.",
                CompanionDeploymentCardTone.Neutral,
                false, false, false, null),
            CompanionDeploymentScreenAction.None, null, false,
            CompanionDeploymentScreenAction.None, null, false);
        _deploymentView.Render(initial, _ => Task.CompletedTask);
    }
}
