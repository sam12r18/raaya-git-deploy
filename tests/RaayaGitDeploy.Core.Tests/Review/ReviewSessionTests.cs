using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Core.Review;

namespace RaayaGitDeploy.Core.Tests.Review;

public sealed class ReviewSessionTests
{
    [Fact]
    public void ApprovingItem_DoesNotAutomaticallySelectItForDeployment()
    {
        var session = CreateSession();

        session.SetReviewState("src/app.cs", ReviewState.Approved);

        var item = Assert.Single(session.Items);
        Assert.Equal(ReviewState.Approved, item.ReviewState);
        Assert.False(item.IsSelectedForDeployment);
    }

    [Fact]
    public void SelectingUnreviewedItem_DoesNotChangeReviewState()
    {
        var session = CreateSession();

        session.SetDeploymentSelected("src/app.cs", true);

        var item = Assert.Single(session.Items);
        Assert.Equal(ReviewState.Unreviewed, item.ReviewState);
        Assert.True(item.IsSelectedForDeployment);
    }

    [Fact]
    public void ExcludingItem_ClearsDeploymentSelection()
    {
        var session = CreateSession();
        session.SetDeploymentSelected("src/app.cs", true);

        session.SetReviewState("src/app.cs", ReviewState.Excluded);

        var item = Assert.Single(session.Items);
        Assert.Equal(ReviewState.Excluded, item.ReviewState);
        Assert.False(item.IsSelectedForDeployment);
    }

    [Fact]
    public void MutatingUnknownPath_ThrowsKeyNotFoundException()
    {
        var session = CreateSession();

        Assert.Throws<KeyNotFoundException>(() =>
            session.SetReviewState("src/missing.cs", ReviewState.Approved));
        Assert.Throws<KeyNotFoundException>(() =>
            session.SetDeploymentSelected("src/missing.cs", true));
    }

    private static ReviewSession CreateSession()
    {
        return ReviewSession.Create(
        [
            new GitChange("src/app.cs", GitChangeKind.Modified)
        ]);
    }
}
