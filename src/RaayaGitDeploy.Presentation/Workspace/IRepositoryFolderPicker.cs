namespace RaayaGitDeploy.Presentation.Workspace;

public interface IRepositoryFolderPicker
{
    Task<string?> PickFolderAsync(CancellationToken cancellationToken);
}
