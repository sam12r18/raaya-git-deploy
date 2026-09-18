using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.Presentation.Tests.Deployment;

public sealed class DeploymentQueueViewModelTests
{
    [Fact]
    public void AddGitSelection_NormalizesDuplicates_WithoutMutatingReviewSelection()
    {
        var viewModel = new DeploymentQueueViewModel();
        var reviewSelection = new[] { "src/App.cs", "README.md" };

        viewModel.AddGitSelection("src/App.cs");
        viewModel.AddGitSelection(@"src\.\App.cs");

        var item = Assert.Single(viewModel.Items);
        Assert.Equal("src/App.cs", item.LocalPath.Replace('\\', '/'));
        Assert.Equal(DeploymentQueueSource.GitSelection, item.Source);
        Assert.Equal(reviewSelection, new[] { "src/App.cs", "README.md" });
    }

    [Fact]
    public void ManualItems_RecordSource_AndSupportRemoveAndClear()
    {
        var viewModel = new DeploymentQueueViewModel();

        viewModel.AddFile("assets/app.js");
        viewModel.AddFolder("assets/css");

        Assert.Collection(
            viewModel.Items,
            item => Assert.Equal(DeploymentQueueSource.ManualFile, item.Source),
            item => Assert.Equal(DeploymentQueueSource.ManualFolder, item.Source));

        viewModel.Remove(viewModel.Items[0]);
        Assert.Single(viewModel.Items);

        viewModel.Clear();
        Assert.Empty(viewModel.Items);
    }

    [Fact]
    public void QueueItem_LeavesRemoteMappingForLaterPlanning()
    {
        var viewModel = new DeploymentQueueViewModel();

        viewModel.AddGitSelection("src/App.cs");

        Assert.Null(Assert.Single(viewModel.Items).RemotePath);
    }
}
