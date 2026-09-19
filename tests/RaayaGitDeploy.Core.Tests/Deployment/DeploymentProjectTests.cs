using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Core.Tests.Deployment;

public sealed class DeploymentProjectTests
{
    [Fact]
    public void Create_AddsProductionProtectedPathsByDefault()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "raaya-project"));

        var project = DeploymentProject.Create(
            "shop-production",
            "Shop Production",
            repositoryRoot,
            "origin",
            "main",
            "cpanel-production",
            "/home/example/core",
            "/home/example/public_html",
            DeploymentStrategy.Laravel);

        Assert.True(project.IsProtected(".env"));
        Assert.True(project.IsProtected("storage/logs/laravel.log"));
        Assert.True(project.IsProtected("uploads/products/a.webp"));
        Assert.False(project.IsProtected("app/Http/Controllers/HomeController.php"));
    }

    [Fact]
    public void Create_RejectsRemoteTraversal()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "raaya-project"));

        Assert.Throws<ArgumentException>(() => DeploymentProject.Create(
            "shop-production",
            "Shop Production",
            repositoryRoot,
            "origin",
            "main",
            "cpanel-production",
            "/home/example/../other"));
    }

    [Fact]
    public void Create_NormalizesCustomProtectedPaths()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "raaya-project"));

        var project = DeploymentProject.Create(
            "shop-production",
            "Shop Production",
            repositoryRoot,
            "origin",
            "main",
            "cpanel-production",
            "/home/example/core",
            protectedPaths: ["public/user-content/"]);

        Assert.True(project.IsProtected("public/user-content/avatar.jpg"));
    }
}
