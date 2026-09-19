using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.Presentation.Tests.Deployment;

public sealed class DeploymentDryRunViewModelTests
{
    [Fact]
    public void Preview_MapsQueueItemsAgainstSelectedServer_WithoutRemoteMutation()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "raaya-workbench-repo"));
        var queue = new DeploymentQueueViewModel();
        queue.AddFile(Path.Combine(repositoryRoot, "src", "App.cs"));
        queue.AddFile(Path.Combine(repositoryRoot, "assets", "app.js"));

        var profile = Profile("prod", "Production");
        var viewModel = new DeploymentDryRunViewModel(new DeploymentPlanner());

        var plan = viewModel.Preview(repositoryRoot, profile, queue.Items);

        Assert.True(plan.IsDryRun);
        Assert.Collection(
            plan.Operations,
            operation => Assert.Equal("/var/www/app/src/App.cs", operation.RemotePath),
            operation => Assert.Equal("/var/www/app/assets/app.js", operation.RemotePath));
    }

    [Fact]
    public void Preview_RequiresSelectedServer()
    {
        var viewModel = new DeploymentDryRunViewModel(new DeploymentPlanner());

        Assert.Throws<InvalidOperationException>(() =>
            viewModel.Preview("C:/repo", null, []));
    }

    [Fact]
    public void Preview_RejectsQueuePathOutsideRepository()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "raaya-workbench-repo"));
        var outsidePath = Path.GetFullPath(Path.Combine(repositoryRoot, "..", "secret.txt"));
        var queueItem = new DeploymentQueueItem(outsidePath, DeploymentQueueSource.ManualFile);
        var viewModel = new DeploymentDryRunViewModel(new DeploymentPlanner());

        Assert.Throws<InvalidOperationException>(() =>
            viewModel.Preview(repositoryRoot, Profile("prod", "Production"), [queueItem]));
    }

    private static ServerProfile Profile(string id, string name) =>
        new(id, name, "example.test", 22, "deploy", "/var/www/app", ServerAuthenticationMode.SshKey, "key-ref");
}
