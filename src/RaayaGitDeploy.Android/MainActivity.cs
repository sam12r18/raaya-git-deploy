using Android.App;
using Android.OS;
using Android.Widget;
using RaayaGitDeploy.Android.Core.Shell;

namespace RaayaGitDeploy.Android;

[Activity(Label = "Raaya Git Deploy", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    private DeploymentScreenView? _deploymentView;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _deploymentView = new DeploymentScreenView(this);
        SetContentView(_deploymentView);

        var initial = new CompanionDeploymentScreenState(
            new CompanionDeploymentCard("Deployment", "Ready", "Connect to the authorized companion agent to review a deployment.", CompanionDeploymentCardTone.Neutral, false, false, false, null),
            CompanionDeploymentScreenAction.None, null, false,
            CompanionDeploymentScreenAction.None, null, false);
        _deploymentView.Render(initial, _ => Task.CompletedTask);
    }
}
