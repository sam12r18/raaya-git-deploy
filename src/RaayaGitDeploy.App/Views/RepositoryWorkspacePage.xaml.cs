using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
using RaayaGitDeploy.Core.Review;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.App.Views;

public sealed partial class RepositoryWorkspacePage : Page
{
    private readonly Window _window;

    public RepositoryWorkspacePage(
        Window window,
        RepositoryWorkspaceViewModel viewModel)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    public RepositoryWorkspaceViewModel ViewModel { get; }

    private async void OpenRepository_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsBusy)
        {
            return;
        }

        var picker = new FolderPicker(_window.AppWindow.Id)
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            CommitButtonText = "Open Repository",
            ViewMode = PickerViewMode.List
        };

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        await ViewModel.OpenRepositoryAsync(folder.Path, CancellationToken.None);
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
        if (e.PropertyName != nameof(RepositoryWorkspaceViewModel.ErrorMessage))
        {
            return;
        }

        ErrorInfoBar.Message = ViewModel.ErrorMessage ?? string.Empty;
        ErrorInfoBar.IsOpen = !string.IsNullOrWhiteSpace(ViewModel.ErrorMessage);
    }
}
