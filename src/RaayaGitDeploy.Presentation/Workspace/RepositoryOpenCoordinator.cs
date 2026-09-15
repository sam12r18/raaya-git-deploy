namespace RaayaGitDeploy.Presentation.Workspace;

public sealed class RepositoryOpenCoordinator
{
    private readonly IRepositoryFolderPicker _folderPicker;
    private readonly RepositoryWorkspaceViewModel _viewModel;

    public RepositoryOpenCoordinator(
        IRepositoryFolderPicker folderPicker,
        RepositoryWorkspaceViewModel viewModel)
    {
        _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    public async Task OpenRepositoryAsync(CancellationToken cancellationToken = default)
    {
        if (_viewModel.IsBusy)
        {
            return;
        }

        string? path;
        try
        {
            path = await _folderPicker.PickFolderAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _viewModel.ReportError(exception);
            return;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await _viewModel.OpenRepositoryAsync(path, cancellationToken);
    }
}
