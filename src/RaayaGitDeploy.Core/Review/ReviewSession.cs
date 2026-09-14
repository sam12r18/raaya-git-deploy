using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Core.Review;

public sealed class ReviewSession
{
    private readonly Dictionary<string, ReviewItem> _itemsByPath;

    private ReviewSession(IReadOnlyList<ReviewItem> items)
    {
        Items = items;
        _itemsByPath = items.ToDictionary(
            static item => item.Path,
            StringComparer.Ordinal);
    }

    public IReadOnlyList<ReviewItem> Items { get; }

    public static ReviewSession Create(IEnumerable<GitChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var items = changes
            .Select(static change => new ReviewItem(change))
            .ToArray();

        return new ReviewSession(items);
    }

    public void SetReviewState(string path, ReviewState state)
    {
        var item = GetRequiredItem(path);
        item.ReviewState = state;

        if (state == ReviewState.Excluded)
        {
            item.IsSelectedForDeployment = false;
        }
    }

    public void SetDeploymentSelected(string path, bool isSelected)
    {
        var item = GetRequiredItem(path);
        item.IsSelectedForDeployment = isSelected;
    }

    private ReviewItem GetRequiredItem(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!_itemsByPath.TryGetValue(path, out var item))
        {
            throw new KeyNotFoundException($"No review item exists for path '{path}'.");
        }

        return item;
    }
}
