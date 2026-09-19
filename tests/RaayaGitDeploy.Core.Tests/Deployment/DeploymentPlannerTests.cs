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
    public void ProjectPlan_MarksProtectedPathsAsSkipped()
    {
        var planner = new DeploymentPlanner();
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repo"));
        var project = DeploymentProject.Create(
            "shop-production",
            "Shop Production",
            repositoryRoot,
            "origin",
            "main",
            "cpanel-production",
            "/home/example/core",
            "/home/example/public_html",
            DeploymentStrategy.Laravel,
            ["public/user-content"]);

        var envPath = Path.Combine(repositoryRoot, ".env");
        var storagePath = Path.Combine(repositoryRoot, "storage", "logs", "laravel.log");
        var uploadPath = Path.Combine(repositoryRoot, "public", "user-content", "avatar.webp");
        var sourcePath = Path.Combine(repositoryRoot, "app", "Http", "Controllers", "HomeController.php");

        var plan = planner.Plan(project, [envPath, storagePath, uploadPath, sourcePath], dryRun: true);

        Assert.True(plan.IsDryRun);
        Assert.Equal(4, plan.Operations.Count);
        Assert.Equal(DeploymentOperationKind.Skip, plan.Operations[0].Kind);
        Assert.Equal(DeploymentOperationKind.Skip, plan.Operations[1].Kind);
        Assert.Equal(DeploymentOperationKind.Skip, plan.Operations[2].Kind);
        Assert.Equal(DeploymentOperationKind.Upload, plan.Operations[3].Kind);
    }
}
