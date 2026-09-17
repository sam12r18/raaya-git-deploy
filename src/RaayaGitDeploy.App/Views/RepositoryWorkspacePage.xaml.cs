using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RaayaGitDeploy.Core.Review;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.App.Views;

public sealed partial class RepositoryWorkspacePage : Page
{
    private readonly RepositoryOpenCoordinator _openCoordinator;

    public RepositoryWorkspacePage(
        RepositoryWorkspaceViewModel viewModel,
        RepositoryOpenCoordinator openCoordinator)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _openCoordinator = openCoordinator ?? throw new ArgumentNullException(nameof(openCoordinator));

        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        WorkbenchNavigation.SelectedItem = WorkbenchNavigation.MenuItems[0];
        UpdateSectionSurface();
    }

    public RepositoryWorkspaceViewModel ViewModel { get; }

    private async void OpenRepository_Click(object sender, RoutedEventArgs e)
    {
        await _openCoordinator.OpenRepositoryAsync(CancellationToken.None);
    }

    private async void WorkingTree_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsBusy || string.IsNullOrWhiteSpace(ViewModel.RepositoryPath))
        {
            return;
        }

        await ViewModel.OpenRepositoryAsync(ViewModel.RepositoryPath, CancellationToken.None);
    }

    private async void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsBusy || string.IsNullOrWhiteSpace(BaseRefTextBox.Text))
        {
            return;
        }

        await ViewModel.CompareSinceAsync(BaseRefTextBox.Text, CancellationToken.None);
    }

    private void WorkbenchNavigation_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag ||
            !Enum.TryParse<WorkspaceSection>(tag, out var section))
        {
            return;
        }

        ViewModel.SelectedSection = section;
        UpdateSectionSurface();
    }

    private void UpdateSectionSurface()
    {
        var showChanges = ViewModel.SelectedSection == WorkspaceSection.Changes;
        ChangesWorkspace.Visibility = showChanges ? Visibility.Visible : Visibility.Collapsed;
        SectionPlaceholder.Visibility = showChanges ? Visibility.Collapsed : Visibility.Visible;
        SectionTitle.Text = ViewModel.SelectedSection switch
        {
            WorkspaceSection.DeployQueue => "Deploy Queue",
            _ => ViewModel.SelectedSection.ToString(),
        };
    }

    private async void ChangesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.IsBusy || ChangesList.SelectedItem is not ChangeItemViewModel item)
        {
            return;
        }

        await ViewModel.LoadDiffAsync(item, CancellationToken.None);
    }

    private void ReviewStateComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.DataContext is ChangeItemViewModel item)
        {
            comboBox.SelectedIndex = (int)item.ReviewState;
        }
    }

    private void ReviewStateComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox ||
            comboBox.DataContext is not ChangeItemViewModel item ||
            comboBox.SelectedIndex < 0)
        {
            return;
        }

        item.ReviewState = (ReviewState)comboBox.SelectedIndex;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RepositoryWorkspaceViewModel.SelectedSection))
        {
            UpdateSectionSurface();
            return;
        }

        if (e.PropertyName != nameof(RepositoryWorkspaceViewModel.ErrorMessage))
        {
            return;
        }

        ErrorInfoBar.Message = ViewModel.ErrorMessage ?? string.Empty;
        ErrorInfoBar.IsOpen = !string.IsNullOrWhiteSpace(ViewModel.ErrorMessage);
    }
}
