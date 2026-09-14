using CommunityToolkit.Mvvm.ComponentModel;
using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Core.Review;

namespace RaayaGitDeploy.Presentation.Workspace;

public sealed class ChangeItemViewModel : ObservableObject
{
    private readonly ReviewSession _session;
    private readonly ReviewItem _item;
    private ReviewState _reviewState;
    private bool _isSelectedForDeployment;

    internal ChangeItemViewModel(
        ReviewSession session,
        ReviewItem item,
        bool isStaged = false,
        bool isUnstaged = false)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _item = item ?? throw new ArgumentNullException(nameof(item));
        _reviewState = item.ReviewState;
        _isSelectedForDeployment = item.IsSelectedForDeployment;
        IsStaged = isStaged;
        IsUnstaged = isUnstaged;
    }

    public string Path => _item.Change.Path;

    public string? OriginalPath => _item.Change.OriginalPath;

    public GitChangeKind Kind => _item.Change.Kind;

    public bool IsStaged { get; }

    public bool IsUnstaged { get; }

    public ReviewState ReviewState
    {
        get => _reviewState;
        set
        {
            if (_reviewState == value)
            {
                return;
            }

            _session.SetReviewState(Path, value);
            SetProperty(ref _reviewState, _item.ReviewState);
            SetProperty(
                ref _isSelectedForDeployment,
                _item.IsSelectedForDeployment,
                nameof(IsSelectedForDeployment));
        }
    }

    public bool IsSelectedForDeployment
    {
        get => _isSelectedForDeployment;
        set
        {
            if (_isSelectedForDeployment == value)
            {
                return;
            }

            _session.SetDeploymentSelected(Path, value);
            SetProperty(ref _isSelectedForDeployment, _item.IsSelectedForDeployment);
        }
    }
}
