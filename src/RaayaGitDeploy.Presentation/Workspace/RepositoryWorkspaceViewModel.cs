using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Presentation.Workspace;

public partial class RepositoryWorkspaceViewModel : ObservableObject
{
    private readonly IGitRepositoryService _repositoryService;

    [ObservableProperty]
    private string? repositoryPath;

    [ObservableProperty]
    private string? branchName;

    [ObservableProperty]
    private string? headSha;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public RepositoryWorkspaceViewModel(IGitRepositoryService repositoryService)
    {
        _repositoryService = repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
    }

    public ObservableCollection<RepositoryChangeItemViewModel> Changes { get; } = new();

    public async Task LoadRepositoryAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var context = await _repositoryService.GetContextAsync(path, cancellationToken);
            var changes = await _repositoryService.GetWorkingTreeChangesAsync(
                context.RootPath,
                cancellationToken);

            RepositoryPath = context.RootPath;
            BranchName = context.BranchName;
            HeadSha = context.HeadSha;

            Changes.Clear();
            foreach (var change in changes)
            {
                Changes.Add(new RepositoryChangeItemViewModel(change));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
