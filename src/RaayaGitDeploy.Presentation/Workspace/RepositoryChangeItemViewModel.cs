using CommunityToolkit.Mvvm.ComponentModel;
using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Presentation.Workspace;

public partial class RepositoryChangeItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isSelectedForDeploy;

    public RepositoryChangeItemViewModel(GitWorkingTreeChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        Path = change.Path;
        Kind = change.Kind;
        OriginalPath = change.OriginalPath;
        IsStaged = change.IsStaged;
        IsUnstaged = change.IsUnstaged;
    }

    public string Path { get; }

    public GitChangeKind Kind { get; }

    public string? OriginalPath { get; }

    public bool IsStaged { get; }

    public bool IsUnstaged { get; }
}
