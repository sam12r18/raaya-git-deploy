using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Core.Review;
using RaayaGitDeploy.Presentation.Terminal;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.App.Views;

public sealed partial class RepositoryWorkspacePage : Page
{
    private readonly RepositoryOpenCoordinator _openCoordinator;
    private readonly TerminalViewModel _terminal;
    private readonly DispatcherTimer _terminalRefreshTimer;
    private string? _terminalRepository;

    public RepositoryWorkspacePage(RepositoryWorkspaceViewModel viewModel, TerminalViewModel terminalViewModel, RepositoryOpenCoordinator openCoordinator)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _terminal = terminalViewModel ?? throw new ArgumentNullException(nameof(terminalViewModel));
        _openCoordinator = openCoordinator ?? throw new ArgumentNullException(nameof(openCoordinator));
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        WorkbenchNavigation.SelectedItem = WorkbenchNavigation.MenuItems[0];
        _terminalRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _terminalRefreshTimer.Tick += (_, _) => RefreshTerminalSurface();
        _terminalRefreshTimer.Start();
        Unloaded += async (_, _) => { _terminalRefreshTimer.Stop(); await _terminal.DisposeAsync(); };
        UpdateSectionSurface();
    }

    public RepositoryWorkspaceViewModel ViewModel { get; }

    private async void OpenRepository_Click(object sender, RoutedEventArgs e) => await _openCoordinator.OpenRepositoryAsync(CancellationToken.None);

    private async void WorkingTree_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsBusy || string.IsNullOrWhiteSpace(ViewModel.RepositoryPath)) return;
        await ViewModel.OpenRepositoryAsync(ViewModel.RepositoryPath, CancellationToken.None);
    }

    private async void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsBusy || string.IsNullOrWhiteSpace(BaseRefTextBox.Text)) return;
        await ViewModel.CompareSinceAsync(BaseRefTextBox.Text, CancellationToken.None);
    }

    private void WorkbenchNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag || !Enum.TryParse<WorkspaceSection>(tag, out var section)) return;
        ViewModel.SelectedSection = section;
        UpdateSectionSurface();
    }

    private void UpdateSectionSurface()
    {
        ChangesWorkspace.Visibility = Visibility.Collapsed; CommitsWorkspace.Visibility = Visibility.Collapsed; TerminalWorkspace.Visibility = Visibility.Collapsed;
        CommandsWorkspace.Visibility = Visibility.Collapsed; DeployQueueWorkspace.Visibility = Visibility.Collapsed; ServersWorkspace.Visibility = Visibility.Collapsed; HistoryWorkspace.Visibility = Visibility.Collapsed;
        switch (ViewModel.SelectedSection)
        {
            case WorkspaceSection.Changes: ChangesWorkspace.Visibility = Visibility.Visible; break;
            case WorkspaceSection.Commits: CommitsWorkspace.Visibility = Visibility.Visible; break;
            case WorkspaceSection.Terminal: TerminalWorkspace.Visibility = Visibility.Visible; break;
            case WorkspaceSection.Commands: CommandsWorkspace.Visibility = Visibility.Visible; break;
            case WorkspaceSection.DeployQueue: DeployQueueWorkspace.Visibility = Visibility.Visible; break;
            case WorkspaceSection.Servers: ServersWorkspace.Visibility = Visibility.Visible; break;
            case WorkspaceSection.History: HistoryWorkspace.Visibility = Visibility.Visible; break;
            default: throw new ArgumentOutOfRangeException();
        }
    }

    private async void TerminalStart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ViewModel.RepositoryPath)) throw new InvalidOperationException("Open a repository before starting the terminal.");
            if (!string.Equals(_terminalRepository, ViewModel.RepositoryPath, StringComparison.OrdinalIgnoreCase))
            {
                await _terminal.SwitchRepositoryAsync(ViewModel.RepositoryPath, CancellationToken.None);
                _terminalRepository = ViewModel.RepositoryPath;
            }
            await _terminal.StartAsync(CancellationToken.None);
            RefreshTerminalSurface();
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private async void TerminalSend_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(TerminalInput.Text)) return;
            var command = TerminalInput.Text;
            TerminalInput.Text = string.Empty;
            await _terminal.SendAsync(command, CancellationToken.None);
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private async void TerminalStop_Click(object sender, RoutedEventArgs e)
    {
        try { await _terminal.StopAsync(CancellationToken.None); RefreshTerminalSurface(); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void RefreshTerminalSurface()
    {
        if (TerminalOutput is null) return;
        if (TerminalOutput.Text != _terminal.Output) TerminalOutput.Text = _terminal.Output;
        TerminalState.Text = _terminal.IsRunning ? "Running" : _terminal.ExitCode is int code ? $"Exited ({code})" : "Stopped";
    }

    private void ChangesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.IsBusy || ChangesList.SelectedItem is not ChangeItemViewModel item) return;
        _ = LoadDiffAsync(item);
    }

    private async Task LoadDiffAsync(ChangeItemViewModel item) => await ViewModel.LoadDiffAsync(item, CancellationToken.None);

    private async void CommitsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.IsBusy || CommitsList.SelectedItem is not GitCommitInfo commit) return;
        CommitFilesList.SelectedItem = null;
        await ViewModel.SelectCommitAsync(commit, CancellationToken.None);
    }

    private async void CommitFilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.IsBusy || CommitFilesList.SelectedItem is not GitChange file) return;
        await ViewModel.SelectCommitFileAsync(file, CancellationToken.None);
    }

    private void ReviewStateComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.DataContext is ChangeItemViewModel item) comboBox.SelectedIndex = (int)item.ReviewState;
    }

    private void ReviewStateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.DataContext is not ChangeItemViewModel item || comboBox.SelectedIndex < 0) return;
        item.ReviewState = (ReviewState)comboBox.SelectedIndex;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RepositoryWorkspaceViewModel.SelectedSection)) { UpdateSectionSurface(); return; }
        if (e.PropertyName != nameof(RepositoryWorkspaceViewModel.ErrorMessage)) return;
        ShowError(ViewModel.ErrorMessage ?? string.Empty);
    }

    private void ShowError(string message)
    {
        ErrorInfoBar.Message = message;
        ErrorInfoBar.IsOpen = !string.IsNullOrWhiteSpace(message);
    }
}
