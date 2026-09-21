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

            // Do not let the workspace move to another repository while an execution is still using
            // the current repository/profile/queue snapshot. ResetForRepositoryChange also guards this,
            // but checking before OpenRepositoryAsync avoids switching the Git workspace first and only
            // then discovering that deployment state cannot safely follow it.
            if (_deployment?.IsExecuting == true)
            {
                _viewModel.ReportError(new InvalidOperationException(
                    "Wait for the active deployment to finish before switching repositories."));
                return;
            }

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
            // Repository paths came from the workspace/folder picker. If normalization ever fails, fail safe and
            // treat the selection as a repository change so stale deployment state cannot survive the transition.
            return false;
        }
    }
}
