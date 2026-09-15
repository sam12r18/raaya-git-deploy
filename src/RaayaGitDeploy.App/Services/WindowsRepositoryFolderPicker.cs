using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.App.Services;

public sealed class WindowsRepositoryFolderPicker : IRepositoryFolderPicker
{
    private readonly Window _window;

    public WindowsRepositoryFolderPicker(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var picker = new FolderPicker(_window.AppWindow.Id)
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            CommitButtonText = "Open Repository",
            ViewMode = PickerViewMode.List
        };

        var folder = await picker.PickSingleFolderAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return folder?.Path;
    }
}
