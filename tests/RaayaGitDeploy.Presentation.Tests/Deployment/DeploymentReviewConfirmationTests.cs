using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.Presentation.Tests.Deployment;

public sealed class DeploymentReviewConfirmationTests
{
    [Fact]
    public void From_destructive_review_lists_target_root_and_each_delete_path()
    {
        var operations = new[]
        {
            new DeploymentOperation(DeploymentOperationKind.Upload, @"C:\repo\public\app.js", "/public/app.js"),
            new DeploymentOperation(DeploymentOperationKind.Delete, string.Empty, "/public/old.js"),
            new DeploymentOperation(DeploymentOperationKind.Delete, string.Empty, "/public/legacy.css")
        };
        var review = new DeploymentReviewSnapshot("prod", "Production", "/home/site", operations, 1, 2, new[] { "/public/old.js", "/public/legacy.css" });

        var confirmation = DeploymentReviewConfirmation.From(review);

        Assert.True(confirmation.IsDestructive);
        Assert.Equal("Confirm destructive deployment", confirmation.Title);
        Assert.Equal("Deploy and delete", confirmation.PrimaryActionText);
        Assert.Contains("Production", confirmation.Detail);
        Assert.Contains("/home/site", confirmation.Detail);
        Assert.Contains("/public/old.js", confirmation.Detail);
        Assert.Contains("/public/legacy.css", confirmation.Detail);
    }

    [Fact]
    public void From_upload_only_review_uses_normal_deploy_action()
    {
        var operations = new[]
        {
            new DeploymentOperation(DeploymentOperationKind.Upload, @"C:\repo\index.html", "/index.html")
        };
        var review = new DeploymentReviewSnapshot("stage", "Staging", "/site", operations, 1, 0, Array.Empty<string>());

        var confirmation = DeploymentReviewConfirmation.From(review);

        Assert.False(confirmation.IsDestructive);
        Assert.Equal("Confirm deployment", confirmation.Title);
        Assert.Equal("Deploy", confirmation.PrimaryActionText);
        Assert.DoesNotContain("Remote paths that will be deleted", confirmation.Detail);
    }
}
