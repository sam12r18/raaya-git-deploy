using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.Presentation.Workspace;

public sealed class RepositoryOpenCoordinator
{
    private readonly IRepositoryFolderPicker _folderPicker;
    private readonly RepositoryWorkspaceViewModel _viewModel;
    private readonly DeploymentWorkspaceViewModel? _deployment;
    private int _openAttemptInProgress;

    public RepositoryOpenCoordinator(
        IRepositoryFolderPicker folderPicker,
        RepositoryWorkspaceViewModel viewModel,
        DeploymentWorkspaceViewModel? deployment = null)
    {
        _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _deployment = deployment;
    }

    public async Task OpenRepositoryAsync(CancellationToken cancellationToken = default)
    {
        if (_viewModel.IsBusy || Interlocked.CompareExchange(ref _openAttemptInProgress, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _viewModel.ClearError();

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

            var previousRepository = _viewModel.RepositoryPath;
            await _viewModel.OpenRepositoryAsync(path, cancellationToken);

            if (_deployment is not null &&
                !string.IsNullOrWhiteSpace(_viewModel.RepositoryPath) &&
                !string.Equals(previousRepository, _viewModel.RepositoryPath, StringComparison.OrdinalIgnoreCase))
            {
                _deployment.ResetForRepositoryChange();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _openAttemptInProgress, 0);
        }
    }
}
