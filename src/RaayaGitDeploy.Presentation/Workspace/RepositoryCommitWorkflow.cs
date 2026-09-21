using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Presentation.Workspace;

/// <summary>
/// Coordinates explicit Git mutations for the currently opened repository and
/// refreshes the read model after every successful mutation.
/// </summary>
public sealed class RepositoryCommitWorkflow
{
    private readonly IGitMutationService _mutationService;
    private readonly RepositoryWorkspaceViewModel _workspace;

    public RepositoryCommitWorkflow(IGitMutationService mutationService, RepositoryWorkspaceViewModel workspace)
    {
        _mutationService = mutationService ?? throw new ArgumentNullException(nameof(mutationService));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public async Task StageAsync(IReadOnlyList<ChangeItemViewModel> changes, CancellationToken cancellationToken = default)
    {
        var (repositoryPath, paths) = RequireSelection(changes);
        await _mutationService.StageAsync(repositoryPath, paths, cancellationToken);
        await _workspace.RefreshCurrentRepositoryAsync(cancellationToken);
    }

    public async Task UnstageAsync(IReadOnlyList<ChangeItemViewModel> changes, CancellationToken cancellationToken = default)
    {
        var (repositoryPath, paths) = RequireSelection(changes);
        await _mutationService.UnstageAsync(repositoryPath, paths, cancellationToken);
        await _workspace.RefreshCurrentRepositoryAsync(cancellationToken);
    }

    public async Task<string> CommitAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var repositoryPath = RequireRepository();
        var sha = await _mutationService.CommitStagedAsync(repositoryPath, message.Trim(), cancellationToken);
        await _workspace.RefreshCurrentRepositoryAsync(cancellationToken);
        return sha;
    }

    private (string RepositoryPath, string[] Paths) RequireSelection(IReadOnlyList<ChangeItemViewModel> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0)
            throw new InvalidOperationException("Select at least one changed file before staging or unstaging.");

        return (RequireRepository(), changes.Select(static change => change.Path).Distinct(StringComparer.Ordinal).ToArray());
    }

    private string RequireRepository() =>
        !string.IsNullOrWhiteSpace(_workspace.RepositoryPath)
            ? _workspace.RepositoryPath
            : throw new InvalidOperationException("Open a Git repository before changing the index or creating a commit.");
}
