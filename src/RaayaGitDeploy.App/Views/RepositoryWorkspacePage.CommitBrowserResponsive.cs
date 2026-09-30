using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace RaayaGitDeploy.App.Views;

public sealed partial class RepositoryWorkspacePage
{
    private const double CommitBrowserCompactWidth = 900;
    private const double CommitBrowserNarrowWidth = 700;
    private const double ServersCompactWidth = 760;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        CommitsWorkspace.SizeChanged += CommitsWorkspace_SizeChanged;
        ServersWorkspace.SizeChanged += ServersWorkspace_SizeChanged;
        ApplyCommitBrowserResponsiveLayout(CommitsWorkspace.ActualWidth);
        ApplyServersResponsiveLayout(ServersWorkspace.ActualWidth);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        CommitsWorkspace.SizeChanged -= CommitsWorkspace_SizeChanged;
        ServersWorkspace.SizeChanged -= ServersWorkspace_SizeChanged;
        base.OnNavigatedFrom(e);
    }

    private void CommitsWorkspace_SizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyCommitBrowserResponsiveLayout(e.NewSize.Width);

    private void ServersWorkspace_SizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyServersResponsiveLayout(e.NewSize.Width);

    private void ApplyCommitBrowserResponsiveLayout(double width)
    {
        // The commit browser deliberately remains a dense IDE-style master/detail surface.
        // Resize the three panes together rather than allowing their previous fixed minimums
        // to force the repository workspace beyond the available window width.
        var paneGrids = CommitsWorkspace.Children
            .OfType<Grid>()
            .Where(grid => grid.ColumnDefinitions.Count == 3)
            .ToArray();

        if (paneGrids.Length == 0)
            return;

        var minimums = width switch
        {
            < CommitBrowserNarrowWidth => (Commits: 120d, Paths: 90d, Diff: 140d),
            < CommitBrowserCompactWidth => (Commits: 160d, Paths: 110d, Diff: 180d),
            _ => (Commits: 220d, Paths: 140d, Diff: 220d)
        };

        foreach (var grid in paneGrids)
        {
            grid.ColumnDefinitions[0].MinWidth = minimums.Commits;
            grid.ColumnDefinitions[1].MinWidth = minimums.Paths;
            grid.ColumnDefinitions[2].MinWidth = minimums.Diff;
        }
    }

    private void ApplyServersResponsiveLayout(double width)
    {
        if (ServersWorkspace.ColumnDefinitions.Count < 2)
            return;

        // Keep the profile list and editor usable side-by-side while allowing the workspace
        // to shrink with the window instead of enforcing the previous 220 + 320 px floor.
        var compact = width > 0 && width < ServersCompactWidth;
        ServersWorkspace.ColumnDefinitions[0].MinWidth = compact ? 140 : 220;
        ServersWorkspace.ColumnDefinitions[1].MinWidth = compact ? 200 : 320;
    }
}
