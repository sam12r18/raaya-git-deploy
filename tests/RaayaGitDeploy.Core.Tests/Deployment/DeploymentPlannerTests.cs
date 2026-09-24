using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Core.Tests.Deployment;

public sealed class DeploymentPlannerTests
{
    [Fact]
    public void Plan_MapsRepositoryFileUnderRemoteRoot()
    {
        var planner = new DeploymentPlanner();
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repo"));
        var localPath = Path.Combine(repositoryRoot, "src", "App.cs");

        var plan = planner.Plan(repositoryRoot, "/var/www/app", [localPath]);

        var operation = Assert.Single(plan.Operations);
        Assert.Equal(DeploymentOperationKind.Upload, operation.Kind);
        Assert.Equal(localPath, operation.LocalPath);
        Assert.Equal("/var/www/app/src/App.cs", operation.RemotePath);
    }

    [Fact]
    public void Plan_RejectsLocalPathOutsideRepositoryRoot()
    {
        var planner = new DeploymentPlanner();
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repo"));
        var outsidePath = Path.GetFullPath(Path.Combine(repositoryRoot, "..", "secrets.txt"));

        Assert.Throws<InvalidOperationException>(() =>
            planner.Plan(repositoryRoot, "/var/www/app", [outsidePath]));
    }

    [Fact]
    public void Plan_RejectsRemoteRootTraversal()
    {
        var planner = new DeploymentPlanner();
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repo"));
        var localPath = Path.Combine(repositoryRoot, "src", "App.cs");

        Assert.Throws<ArgumentException>(() =>
            planner.Plan(repositoryRoot, "/var/www/../etc", [localPath]));
    }

    [Fact]
    public void DryRun_DoesNotInvokeRemoteMutation()
    {
        var planner = new DeploymentPlanner();
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repo"));
        var localPath = Path.Combine(repositoryRoot, "assets", "site.css");

        var plan = planner.Plan(repositoryRoot, "/srv/site", [localPath], dryRun: true);

        Assert.True(plan.IsDryRun);
        Assert.All(plan.Operations, operation => Assert.Equal(DeploymentOperationKind.Upload, operation.Kind));
    }

    [Fact]
    public void Queue_plan_maps_delete_without_requiring_local_file()
    {
        var planner = new DeploymentPlanner();
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"repo-{Guid.NewGuid():N}"));
        var deletedPath = Path.Combine(repositoryRoot, "gone.php");
        var item = new DeploymentQueueItem(
            deletedPath,
            DeploymentQueueSource.GitDetected,
            Action: DeploymentQueueAction.Delete);

        var plan = planner.Plan(repositoryRoot, "/public_html", [item]);

        var operation = Assert.Single(plan.Operations);
        Assert.Equal(DeploymentOperationKind.Delete, operation.Kind);
        Assert.Equal(deletedPath, operation.LocalPath);
        Assert.Equal("/public_html/gone.php", operation.RemotePath);
    }

    [Fact]
    public void Queue_plan_rejects_delete_path_outside_repository()
    {
        var planner = new DeploymentPlanner();
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"repo-{Guid.NewGuid():N}"));
        var outsidePath = Path.GetFullPath(Path.Combine(repositoryRoot, "..", "outside.php"));
        var item = new DeploymentQueueItem(
            outsidePath,
            DeploymentQueueSource.GitDetected,
            Action: DeploymentQueueAction.Delete);

        Assert.Throws<InvalidOperationException>(() =>
            planner.Plan(repositoryRoot, "/public_html", [item]));
    }
}
