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
            // Reject repository switching before opening the native picker. An active deployment owns the
            // current repository/profile/queue snapshot, so presenting a picker would imply a switch can
            // proceed when it cannot safely do so.
            if (_deployment?.IsExecuting == true)
            {
                _viewModel.ReportError(new InvalidOperationException(
                    "Wait for the active deployment to finish before switching repositories."));
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

            // Cancelling the native picker is not a repository operation. Preserve the complete current
            // workspace state, including any diagnostic the user may still be reading.
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            _viewModel.ClearError();
            var previousRepository = _viewModel.RepositoryPath;
            await _viewModel.OpenRepositoryAsync(path, cancellationToken);

            if (_deployment is not null &&
                !string.IsNullOrWhiteSpace(_viewModel.RepositoryPath) &&
                !AreSameRepository(previousRepository, _viewModel.RepositoryPath))
            {
                _deployment.ResetForRepositoryChange();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _openAttemptInProgress, 0);
        }
    }

    private static bool AreSameRepository(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        try
        {
            var normalizedLeft = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
            var normalizedRight = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
