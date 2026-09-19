using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.Presentation.Tests.Deployment;

public sealed class DeploymentProjectEditorViewModelTests
{
    [Fact]
    public void TryCreate_BuildsSafeProjectFromFormState()
    {
        var viewModel = new DeploymentProjectEditorViewModel
        {
            DisplayName = "Storefront",
            RepositoryRoot = Path.GetFullPath("repo"),
            RemoteName = "origin",
            Branch = "main",
            ServerProfileId = "prod",
            ApplicationRoot = "/home/user/core",
            PublicRoot = "/home/user/public_html",
            Strategy = DeploymentStrategy.Laravel,
            ProtectedPathsText = ".env\nstorage\nuploads\npublic/media"
        };

        var result = viewModel.TryCreate(out var project);

        Assert.True(result);
        Assert.NotNull(project);
        Assert.Equal("main", project.Branch);
        Assert.Equal(DeploymentStrategy.Laravel, project.Strategy);
        Assert.True(project.IsProtected("public/media/photo.jpg"));
        Assert.Null(viewModel.ValidationError);
    }

    [Fact]
    public void TryCreate_RejectsRelativeRemoteRootWithoutThrowingIntoUi()
    {
        var viewModel = new DeploymentProjectEditorViewModel
        {
            DisplayName = "Storefront",
            RepositoryRoot = Path.GetFullPath("repo"),
            ServerProfileId = "prod",
            ApplicationRoot = "public_html"
        };

        var result = viewModel.TryCreate(out var project);

        Assert.False(result);
        Assert.Null(project);
        Assert.Contains("absolute POSIX path", viewModel.ValidationError);
    }
}
