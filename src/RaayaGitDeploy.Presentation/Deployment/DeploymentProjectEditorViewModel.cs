using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Presentation.Deployment;

public sealed class DeploymentProjectEditorViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string RepositoryRoot { get; set; } = string.Empty;
    public string RemoteName { get; set; } = "origin";
    public string Branch { get; set; } = "main";
    public string ServerProfileId { get; set; } = string.Empty;
    public string ApplicationRoot { get; set; } = string.Empty;
    public string PublicRoot { get; set; } = string.Empty;
    public DeploymentStrategy Strategy { get; set; } = DeploymentStrategy.FileSync;
    public string ProtectedPathsText { get; set; } = ".env\nstorage\nuploads";
    public string? ValidationError { get; private set; }

    public bool TryCreate(out DeploymentProject? project)
    {
        try
        {
            var protectedPaths = ProtectedPathsText
                .Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            project = DeploymentProject.Create(
                Guid.NewGuid().ToString("N"),
                DisplayName,
                RepositoryRoot,
                RemoteName,
                Branch,
                ServerProfileId,
                ApplicationRoot,
                string.IsNullOrWhiteSpace(PublicRoot) ? null : PublicRoot,
                Strategy,
                protectedPaths);

            ValidationError = null;
            return true;
        }
        catch (ArgumentException exception)
        {
            project = null;
            ValidationError = exception.Message;
            return false;
        }
    }
}
