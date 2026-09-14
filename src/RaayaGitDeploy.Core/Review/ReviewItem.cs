using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Core.Review;

public sealed class ReviewItem
{
    internal ReviewItem(GitChange change)
    {
        Change = change ?? throw new ArgumentNullException(nameof(change));
    }

    public GitChange Change { get; }

    public string Path => Change.Path;

    public ReviewState ReviewState { get; internal set; } = ReviewState.Unreviewed;

    public bool IsSelectedForDeployment { get; internal set; }
}
