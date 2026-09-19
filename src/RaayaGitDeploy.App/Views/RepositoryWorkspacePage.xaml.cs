using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RaayaGitDeploy.Core.Commands;
using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Core.Review;
using RaayaGitDeploy.Presentation.Commands;
using RaayaGitDeploy.Presentation.Deployment;
using RaayaGitDeploy.Presentation.Terminal;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.App.Views;

public sealed partial class RepositoryWorkspacePage : Page
{
    private readonly RepositoryOpenCoordinator _openCoordinator;
    private readonly TerminalViewModel _terminal;
    private readonly CommandsViewModel _commands;
    private readonly DeploymentWorkspaceViewModel _deployment;
    private readonly DispatcherTimer _terminalRefreshTimer;
    private string? _terminalRepository;

    public RepositoryWorkspacePage(RepositoryWorkspaceViewModel viewModel, TerminalViewModel terminalViewModel, CommandsViewModel commandsViewModel, DeploymentWorkspaceViewModel deploymentViewModel, RepositoryOpenCoordinator openCoordinator)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _terminal = terminalViewModel ?? throw new ArgumentNullException(nameof(terminalViewModel));
        _commands = commandsViewModel ?? throw new ArgumentNullException(nameof(commandsViewModel));
        _deployment = deploymentViewModel ?? throw new ArgumentNullException(nameof(deploymentViewModel));
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
    private async void WorkingTree_Click(object sender, RoutedEventArgs e) { if (!ViewModel.IsBusy && !string.IsNullOrWhiteSpace(ViewModel.RepositoryPath)) await ViewModel.OpenRepositoryAsync(ViewModel.RepositoryPath, CancellationToken.None); }
    private async void Compare_Click(object sender, RoutedEventArgs e) { if (!ViewModel.IsBusy && !string.IsNullOrWhiteSpace(BaseRefTextBox.Text)) await ViewModel.CompareSinceAsync(BaseRefTextBox.Text, CancellationToken.None); }

    private async void WorkbenchNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag || !Enum.TryParse<WorkspaceSection>(tag, out var section)) return;
        ViewModel.SelectedSection = section; UpdateSectionSurface();
        if (section == WorkspaceSection.Commands) await LoadCommandsAsync();
        if (section is WorkspaceSection.Servers or WorkspaceSection.DeployQueue) await LoadDeploymentAsync();
    }

    private void UpdateSectionSurface()
    {
        ChangesWorkspace.Visibility = CommitsWorkspace.Visibility = TerminalWorkspace.Visibility = CommandsWorkspace.Visibility = DeployQueueWorkspace.Visibility = ServersWorkspace.Visibility = HistoryWorkspace.Visibility = Visibility.Collapsed;
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

    private async Task LoadDeploymentAsync() { try { await _deployment.LoadServersAsync(CancellationToken.None); RefreshDeploymentSurface(); } catch (Exception ex) { ShowError(ex.Message); } }
    private void RefreshDeploymentSurface() { DeployQueueList.ItemsSource = null; DeployQueueList.ItemsSource = _deployment.Queue.Items; ServersList.ItemsSource = null; ServersList.ItemsSource = _deployment.Servers.Profiles; ServersList.SelectedItem = _deployment.Servers.SelectedProfile; DryRunList.ItemsSource = _deployment.PreviewPlan?.Operations; }
    private void DeployAddSelectedChange_Click(object sender, RoutedEventArgs e) { try { if (ChangesList.SelectedItem is not ChangeItemViewModel change) throw new InvalidOperationException("Select a changed file first."); if (string.IsNullOrWhiteSpace(ViewModel.RepositoryPath)) throw new InvalidOperationException("Open a repository first."); _deployment.Queue.AddGitSelection(Path.Combine(ViewModel.RepositoryPath, change.Path)); RefreshDeploymentSurface(); } catch (Exception ex) { ShowError(ex.Message); } }
    private void DeployClear_Click(object sender, RoutedEventArgs e) { _deployment.Queue.Clear(); RefreshDeploymentSurface(); }
    private void DryRun_Click(object sender, RoutedEventArgs e) { try { if (string.IsNullOrWhiteSpace(ViewModel.RepositoryPath)) throw new InvalidOperationException("Open a repository first."); _deployment.RefreshDryRunPreview(ViewModel.RepositoryPath); RefreshDeploymentSurface(); } catch (Exception ex) { ShowError(ex.Message); } }
    private void ServersList_SelectionChanged(object sender, SelectionChangedEventArgs e) { _deployment.Servers.SelectedProfile = ServersList.SelectedItem as ServerProfile; if (_deployment.Servers.SelectedProfile is { } p) { ServerName.Text=p.DisplayName; ServerHost.Text=p.Host; ServerPort.Text=p.Port.ToString(); ServerUsername.Text=p.Username; ServerRemoteRoot.Text=p.RemoteRoot; ServerKeyReference.Text=p.KeyReference; } }
    private void ServerNew_Click(object sender, RoutedEventArgs e) { _deployment.Servers.SelectedProfile=null; ServersList.SelectedItem=null; ServerName.Text=ServerHost.Text=ServerUsername.Text=ServerRemoteRoot.Text=ServerKeyReference.Text=string.Empty; ServerPort.Text="22"; }
    private async void ServerSave_Click(object sender, RoutedEventArgs e) { try { if (!int.TryParse(ServerPort.Text, out var port)) throw new InvalidOperationException("Port must be a number."); var profile=new ServerProfile(_deployment.Servers.SelectedProfile?.Id ?? Guid.NewGuid().ToString("N"), ServerName.Text.Trim(), ServerHost.Text.Trim(), port, ServerUsername.Text.Trim(), ServerRemoteRoot.Text.Trim(), ServerAuthenticationMode.SshKey, ServerKeyReference.Text.Trim()); await _deployment.Servers.SaveAsync(profile, CancellationToken.None); RefreshDeploymentSurface(); } catch(Exception ex){ ShowError(ex.Message); } }
    private async void ServerDelete_Click(object sender, RoutedEventArgs e) { try { await _deployment.Servers.DeleteSelectedAsync(CancellationToken.None); ServerNew_Click(sender,e); RefreshDeploymentSurface(); } catch(Exception ex){ ShowError(ex.Message); } }
    private async void ServerTest_Click(object sender, RoutedEventArgs e) { try { await _deployment.Servers.TestSelectedConnectionAsync(CancellationToken.None); } catch(Exception ex){ ShowError(ex.Message); } }

    private async Task LoadCommandsAsync() { try { _commands.SetRepository(ViewModel.RepositoryPath); await _commands.LoadAsync(CancellationToken.None); RefreshCommandsSurface(); } catch (Exception ex) { ShowError(ex.Message); } }
    private void RefreshCommandsSurface() { CommandsList.ItemsSource = null; CommandsList.ItemsSource = _commands.Commands; CommandsList.SelectedItem = _commands.SelectedCommand; }
    private void CommandsList_SelectionChanged(object sender, SelectionChangedEventArgs e) { _commands.SelectedCommand = CommandsList.SelectedItem as SavedCommand; if (_commands.SelectedCommand is not { } c) return; CommandName.Text=c.Name; CommandText.Text=c.CommandText; CommandWorkingDirectory.Text=c.WorkingDirectoryOverride ?? string.Empty; }
    private async void CommandSave_Click(object sender, RoutedEventArgs e) { try { if(string.IsNullOrWhiteSpace(CommandName.Text)||string.IsNullOrWhiteSpace(CommandText.Text)) throw new InvalidOperationException("Command name and command text are required."); var id=_commands.SelectedCommand?.Id ?? Guid.NewGuid().ToString("N"); await _commands.SaveAsync(new SavedCommand(id,CommandName.Text.Trim(),CommandText.Text.Trim(),string.IsNullOrWhiteSpace(CommandWorkingDirectory.Text)?null:CommandWorkingDirectory.Text.Trim()),CancellationToken.None); RefreshCommandsSurface(); } catch(Exception ex){ShowError(ex.Message);} }
    private void CommandNew_Click(object sender,RoutedEventArgs e){_commands.SelectedCommand=null;CommandsList.SelectedItem=null;CommandName.Text=CommandText.Text=CommandWorkingDirectory.Text=string.Empty;}
    private async void CommandDelete_Click(object sender,RoutedEventArgs e){try{await _commands.DeleteSelectedAsync(CancellationToken.None);CommandNew_Click(sender,e);RefreshCommandsSurface();}catch(Exception ex){ShowError(ex.Message);}}
    private async void CommandRun_Click(object sender,RoutedEventArgs e){try{await _commands.RunSelectedAsync(CancellationToken.None);}catch(Exception ex){ShowError(ex.Message);}}

    private async void TerminalStart_Click(object sender,RoutedEventArgs e){try{if(string.IsNullOrWhiteSpace(ViewModel.RepositoryPath))throw new InvalidOperationException("Open a repository before starting the terminal.");if(!string.Equals(_terminalRepository,ViewModel.RepositoryPath,StringComparison.OrdinalIgnoreCase)){await _terminal.SwitchRepositoryAsync(ViewModel.RepositoryPath,CancellationToken.None);_terminalRepository=ViewModel.RepositoryPath;}await _terminal.StartAsync(CancellationToken.None);RefreshTerminalSurface();}catch(Exception ex){ShowError(ex.Message);}}
    private async void TerminalSend_Click(object sender,RoutedEventArgs e){try{if(string.IsNullOrWhiteSpace(TerminalInput.Text))return;var command=TerminalInput.Text;TerminalInput.Text=string.Empty;await _terminal.SendAsync(command,CancellationToken.None);}catch(Exception ex){ShowError(ex.Message);}}
    private async void TerminalStop_Click(object sender,RoutedEventArgs e){try{await _terminal.StopAsync(CancellationToken.None);RefreshTerminalSurface();}catch(Exception ex){ShowError(ex.Message);}}
    private void RefreshTerminalSurface(){if(TerminalOutput is null)return;if(TerminalOutput.Text!=_terminal.Output)TerminalOutput.Text=_terminal.Output;TerminalState.Text=_terminal.IsRunning?"Running":_terminal.ExitCode is int code?$"Exited ({code})":"Stopped";}
    private void ChangesList_SelectionChanged(object sender,SelectionChangedEventArgs e){if(!ViewModel.IsBusy&&ChangesList.SelectedItem is ChangeItemViewModel item)_=ViewModel.LoadDiffAsync(item,CancellationToken.None);}
    private async void CommitsList_SelectionChanged(object sender,SelectionChangedEventArgs e){if(!ViewModel.IsBusy&&CommitsList.SelectedItem is GitCommitInfo commit){CommitFilesList.SelectedItem=null;await ViewModel.SelectCommitAsync(commit,CancellationToken.None);}}
    private async void CommitFilesList_SelectionChanged(object sender,SelectionChangedEventArgs e){if(!ViewModel.IsBusy&&CommitFilesList.SelectedItem is GitChange file)await ViewModel.SelectCommitFileAsync(file,CancellationToken.None);}
    private void ReviewStateComboBox_Loaded(object sender,RoutedEventArgs e){if(sender is ComboBox comboBox&&comboBox.DataContext is ChangeItemViewModel item)comboBox.SelectedIndex=(int)item.ReviewState;}
    private void ReviewStateComboBox_SelectionChanged(object sender,SelectionChangedEventArgs e){if(sender is ComboBox comboBox&&comboBox.DataContext is ChangeItemViewModel item&&comboBox.SelectedIndex>=0)item.ReviewState=(ReviewState)comboBox.SelectedIndex;}
    private void ViewModel_PropertyChanged(object? sender,PropertyChangedEventArgs e){if(e.PropertyName==nameof(RepositoryWorkspaceViewModel.SelectedSection)){UpdateSectionSurface();return;}if(e.PropertyName==nameof(RepositoryWorkspaceViewModel.RepositoryPath))_commands.SetRepository(ViewModel.RepositoryPath);if(e.PropertyName==nameof(RepositoryWorkspaceViewModel.ErrorMessage))ShowError(ViewModel.ErrorMessage??string.Empty);}
    private void ShowError(string message){ErrorInfoBar.Message=message;ErrorInfoBar.IsOpen=!string.IsNullOrWhiteSpace(message);}
}
